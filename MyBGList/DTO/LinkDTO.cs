namespace MyBGList.DTO
{
    public class LinkDTO
    {
        public LinkDTO(string href, string rel, string type)
        {
            Href = href;
            Rel = rel;
            Type = type;
        }

        public string Href { get; set; }
        public string Rel {  get; set; }
        public string Type { get; set; }
    }
}
