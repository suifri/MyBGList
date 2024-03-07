using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBGList.DTO;
using MyBGList.Models;
using System.Linq.Dynamic.Core;
using System.ComponentModel.DataAnnotations;
using MyBGList.Attributes;

namespace MyBGList.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class DomainsController : ControllerBase
    {
        public ApplicationDbContext _context {  get; set; }

        public ILogger<DomainsController> _logger;

        public DomainsController(ApplicationDbContext context, ILogger<DomainsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet(Name = "GetDomains")]
        [ResponseCache(CacheProfileName = "Any-60")]
        [ManualValidationFilter]
        public async Task<ActionResult<RestDTO<Domain[]>>> Get([FromQuery] RequestDTO<Domain> input)
        {
            if(!ModelState.IsValid)
            {
                var details = new ValidationProblemDetails(ModelState);
                details.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier;

                if(ModelState.Keys.Any(k => k == "PageSize"))
                {
                    details.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.2";
                    details.Status = StatusCodes.Status501NotImplemented;
                    return new ObjectResult(details)
                    {
                        StatusCode = StatusCodes.Status501NotImplemented
                    };
                }
                else
                {
                    details.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                    details.Status = StatusCodes.Status400BadRequest;
                    return new BadRequestObjectResult(details);
                }
            }


            var query = _context.Domains.AsQueryable();

            if (!string.IsNullOrEmpty(input.FilterQuery))
                query = query.Where(d => d.Name.Contains(input.FilterQuery));

            var recordCount = await query.CountAsync();

            query = query.OrderBy($"{input.SortColumn} {input.SortOrder}").Skip(input.PageIndex * input.PageSize).Take(input.PageSize);

            return new RestDTO<Domain[]>()
            {
                Data = await query.ToArrayAsync(),
                PageIndex = input.PageIndex,
                PageSize = input.PageSize,
                RecordCount = recordCount,
                Links = new List<LinkDTO> { new LinkDTO(Url.Action(null, "Domains", new { input.PageIndex, input.PageSize }, null, Request.Scheme)!, "self", "GET") }
            };
        }

        [HttpPost(Name = "UpdateDomain")]
        [ResponseCache(CacheProfileName = "NoCache")]
        public async Task<RestDTO<Domain?>> Post(DomainDTO model)
        {
            var domain = await _context.Domains.Where(d => d.Id == model.Id).FirstOrDefaultAsync();

            if(domain != null)
            {
                if(!string.IsNullOrEmpty(model.Name))
                    domain.Name = model.Name;
                domain.LastModifiedDate = DateTime.Now;
                _context.Domains.Update(domain);

                await _context.SaveChangesAsync();
            }

            return new RestDTO<Domain?>()
            {
                Data = domain,
                Links = new List<LinkDTO> { new LinkDTO(Url.Action(null, "Domains", model, Request.Scheme)!, "self", "POST") }
            };
        }

        [HttpDelete(Name = "DeleteDomain")]
        [ResponseCache(CacheProfileName = "NoCache")]
        public async Task<RestDTO<Domain?>> Delete(int id)
        {
            var domain = await _context.Domains.Where(d => d.Id == id).FirstOrDefaultAsync();

            if(domain != null)
            {
                _context.Domains.Remove(domain);
                await _context.SaveChangesAsync();
            }

            return new RestDTO<Domain?>()
            {
                Data = domain,
                Links = new List<LinkDTO> { new LinkDTO(Url.Action(null, "Domains", id, Request.Scheme)!, "self", "DELETE") }
            };
        }
    }
}
