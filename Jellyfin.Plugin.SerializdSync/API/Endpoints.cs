using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SerializdSync.API.Requests;
using Jellyfin.Plugin.SerializdSync.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SerializdSync.API
{
    [ApiController]
    [Authorize(Policy = "RequiresElevation")]
    [Route("Serializd")]
    public class Endpoints : ControllerBase
    {
        private readonly SerializdApi _api;

        public Endpoints(SerializdApi api)
        {
            _api = api;
        }

        [HttpPost("Login")]
        public async Task<ActionResult<LoginResult>> Login([FromBody] PluginLoginRequest request, CancellationToken cancellationToken)
        {
            var login = await _api.LoginAsync(request.Email, request.Password, cancellationToken).ConfigureAwait(false);
            if (login?.Token is null || string.IsNullOrEmpty(login.Token))
            {
                return new LoginResult { Success = false };
            }

            SerializdPlugin.Instance?.Configuration.SetCredentials(
                request.UserId,
                login.Username ?? string.Empty,
                login.Token,
                request.Email,
                request.Password);
            SerializdPlugin.Instance?.SaveConfiguration();

            return new LoginResult { Success = true, Username = login.Username };
        }

        [HttpPost("Logout")]
        public ActionResult Logout([FromBody] PluginLogoutRequest request)
        {
            SerializdPlugin.Instance?.Configuration.ClearCredentials(request.UserId);
            SerializdPlugin.Instance?.SaveConfiguration();
            return NoContent();
        }
    }
}
