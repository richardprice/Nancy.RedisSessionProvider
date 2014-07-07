namespace NancyApplication1
{
    using Nancy;

    public class IndexModule : NancyModule
    {
        public IndexModule()
        {
            Get["/"] = parameters =>
            {
                Session["test1"] = "this is a test";
                return View["index"];
            };
        }
    }
}