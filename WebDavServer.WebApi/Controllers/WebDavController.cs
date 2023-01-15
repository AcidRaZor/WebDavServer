﻿using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebDavServer.Application.Contracts.WebDav;
using WebDavServer.Application.Contracts.WebDav.Models.Request;
using WebDavServer.WebApi.Extensions;

namespace WebDavServer.WebApi.Controllers
{
    [Route("{**path}")]
    public class WebDavController : ControllerBase
    {
        private readonly IWebDavService _webDavService;
        private readonly ILogger<WebDavController> _logger;

        public WebDavController(
            IWebDavService webDavService, ILogger<WebDavController> logger)
        {
            _webDavService = webDavService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync(string? path, CancellationToken cancellationToken = default)
        {
            if (Request.Headers.IsIfLastModify())
            {
                return StatusCode((int)HttpStatusCode.NotModified);
            }

            await using var stream = await _webDavService.GetAsync(path ?? string.Empty, cancellationToken);
            
            await stream.CopyToAsync(Response.Body, cancellationToken);
            
            return Ok();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AcceptVerbs("PROPFIND")]
        public async Task<string> PropfindAsync(string? path, CancellationToken cancellationToken)
        {
            var returnXml = await _webDavService.PropfindAsync(new PropfindRequest
            {
                Url = $"{Request.GetDisplayUrl().TrimEnd('/')}/",
                Path = path ?? string.Empty,
                Depth = Request.Headers.GetDepth()
            }, cancellationToken);

            Response.StatusCode = (int)HttpStatusCode.MultiStatus;

            return returnXml;
        }

        [HttpHead]
        public ActionResult Head(string? path)
        {
            string head = null;

            if (head != null)
            {
                Response.Headers.Add("Last-Modified", DateTime.Now.ToString());
                return Ok();
            }
            else
                return NotFound();
        }

        [HttpOptions]
        public ActionResult Options(string? path)
        {
            var methods = new string[]
            {
                "OPTIONS", "GET", "HEAD", "PROPFIND", "MKCOL", "PUT", "DELETE", "COPY", "MOVE", "LOCK", "UNLOCK", "PROPPATCH"
            };

            Response.Headers.Add("Allow", String.Join(',', methods));
            Response.Headers.Add("DAV", "1,2,extend");

            return Ok();
        }

        [HttpDelete]
        public ActionResult Delete(string? path)
        {
            _webDavService.DeleteAsync(path ?? string.Empty);

            return Ok();
        }

        [HttpPut]
        [DisableRequestSizeLimit]
        public async Task<ActionResult> PutAsync(string? path, CancellationToken cancellationToken)
        {
            var contentLength = Request.ContentLength ?? 0;

            if (contentLength == 0)
            {
                return StatusCode((int)HttpStatusCode.NotModified);
            }
            
            await _webDavService.PutAsync(path ?? string.Empty, Request.Body, cancellationToken);

            return StatusCode((int)HttpStatusCode.Created);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AcceptVerbs("MKCOL")]
        public ActionResult MkCol(string? path, CancellationToken cancellationToken = default)
        {
            _webDavService.MkColAsync(path ?? string.Empty, cancellationToken);

            return StatusCode((int)HttpStatusCode.Created);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AcceptVerbs("MOVE")]
        public async Task<IActionResult> Move(string? path, CancellationToken cancellationToken = default)
        {
            var requestPath = path ?? string.Empty;

            await _webDavService.MoveAsync(requestPath,
                GetPathFromDestination(Request.Headers.GetDestination()), cancellationToken);
            
            return Created(new Uri(requestPath), null);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AcceptVerbs("COPY")]
        public async Task<IActionResult> Copy(string? path, CancellationToken cancellationToken = default)
        {
            var requestPath = path ?? string.Empty;

            await _webDavService.CopyAsync(requestPath,
                GetPathFromDestination(Request.Headers.GetDestination()), cancellationToken);

            return Created(new Uri(requestPath), null);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AcceptVerbs("LOCK")]
        public async Task<string> LockAsync(string? path, CancellationToken cancellationToken = default)
        {
            int timeoutSecond = Request.Headers.GetTimeoutSecond();
            
            var xml = await ReadXmlFromBodyAsync(cancellationToken);

            var response = await _webDavService.LockAsync(new LockRequest()
            {
                Url = Request.GetDisplayUrl(),
                Path = path ?? string.Empty,
                TimeoutSecond = timeoutSecond,
                Xml = xml
            }, cancellationToken);

            Response.Headers.Add("LockAsync-Token", response.LockToken);
            Response.StatusCode = (int)HttpStatusCode.OK;

            return response.Xml;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AcceptVerbs("UNLOCK")]
        public ActionResult Unlock(string? path, CancellationToken cancellationToken = default)
        {
            _webDavService.UnlockAsync(path ?? string.Empty);

            return StatusCode((int)HttpStatusCode.NoContent);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AcceptVerbs("PROPPATCH")]
        public string Propatch(string? path)
        {
            return string.Empty;
        }

        string GetPathFromDestination(string dst)
            => dst.Remove(0, $"{Request.Scheme}://{Request.Host}".Length).Trim('/');

        async Task<string> ReadXmlFromBodyAsync(CancellationToken cancellationToken = default)
        {
            var result = await Request.BodyReader.ReadAsync(cancellationToken);

            return Encoding.UTF8.GetString(result.Buffer.ToArray());
        }
    }
}