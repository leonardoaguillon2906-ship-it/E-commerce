using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;

namespace EcommerceApp.Services
{
    public class EmailTemplateService
    {
        private readonly IWebHostEnvironment _env;

        public EmailTemplateService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> LoadAsync(string templateName)
        {
            var path = Path.Combine(
                _env.ContentRootPath,
                "EmailTemplates",
                templateName
            );

            return await File.ReadAllTextAsync(path);
        }
    }
}
