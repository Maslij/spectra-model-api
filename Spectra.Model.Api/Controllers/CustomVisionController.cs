﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using Spectra.Model.Api.Models;
using Spectra.Model.Api.Services;

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
        public async Task<IList<SpectraProjectWithMetadata>> GetProjects(CustomVisionProject customVisionProject)
        {
            return await _customVisionService.GetProjects(customVisionProject);
        }

        [Route("project/{id}/images/customvision/{iteration}")]
        [HttpGet]
        public async Task<object> GetProjectWithImagesAndRegions(CustomVisionProject customVisionProject, Guid id, Guid iteration)
        {
            var response = await _customVisionService.GetProjectWithImagesAndRegions(customVisionProject, id, iteration);

            return response;
        }

        [Route("project/{id}/images/pascal/{iteration}")]
        [HttpGet]
        public async Task<object> GetProjectWithImageAndPascalAnnotations(CustomVisionProject customVisionProject, Guid id, Guid iteration)
        {
            var response = await _customVisionService.GetProjectWithImageAndPascalAnnotations(customVisionProject, id, iteration);

            return response;
        }

        [Route("project/{id}/images/yolo/{iteration}")]
        [HttpGet]
        public async Task<object> GetProjectWithImageAndYoloAnnotations(CustomVisionProject customVisionProject, Guid id, Guid iteration)
        {
            var response = await _customVisionService.GetProjectWithImageAndYoloAnnotations(customVisionProject, id, iteration);

            return response;
        }

        [Route("project/{id}/images/upload")]
        [HttpPost]
        public async Task<object> UploadImageToAzure(CustomVisionBatchImage customVisionBatchImage, Guid id)
        {
            var response = await _customVisionService.UploadImageToAzure(customVisionBatchImage, id);
            return response;                                          
        }
    }
}
