using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using Spectra.Model.Api.Models;
using Spectra.Model.Api.Services;
using System.IO.Compression;

namespace Spectra.Model.Api.Controllers
{
    [ApiController]
    [Route("api")]
    [Produces("application/json")]
    public class CustomVisionController : Controller
    {
        public CustomVisionService _customVisionService;

        public CustomVisionController(CustomVisionService customVisionService)
        {
            _customVisionService = customVisionService;
        }

        [Route("projects")]
        [HttpGet]
        public async Task<IList<Project>> GetProjects(CustomVisionProject customVisionProject)
        {
            return await _customVisionService.GetProjects(customVisionProject);
        }

        [Route("project/{id}/images")]
        [HttpGet]
        public async Task<object> GetZippedProjectWithImagesAndRegions(CustomVisionProject customVisionProject,Guid id)
        {
            var response = await _customVisionService.GetZippedProjectWithImagesAndRegions(customVisionProject, id);

            return response;
        }
    }
}
