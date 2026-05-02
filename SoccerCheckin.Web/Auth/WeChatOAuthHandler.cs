using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SoccerCheckin.Web.Auth;

public class WeChatOAuthHandler : OAuthHandler<OAuthOptions>
{
    public WeChatOAuthHandler(IOptionsMonitor<OAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticationTicket> CreateTicketAsync(ClaimsIdentity identity, AuthenticationProperties properties, OAuthTokenResponse tokens)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInformationEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await Backchannel.SendAsync(request, HttpCompletionOption.ResponseContentRead, Context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"An error occurred when retrieving WeChat user information ({response.StatusCode}). Please check if the authentication information is correct.");
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        var json = await JsonDocument.ParseAsync(stream);
        var root = json.RootElement;

        var openId = root.GetProperty("openid").GetString() ?? string.Empty;
        var nickname = root.TryGetProperty("nickname", out var nickProp) ? nickProp.GetString() : null;
        var headimgUrl = root.TryGetProperty("headimgurl", out var headProp) ? headProp.GetString() : null;

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, openId));
        identity.AddClaim(new Claim(ClaimTypes.Name, nickname ?? openId));
        if (!string.IsNullOrEmpty(headimgUrl))
        {
            identity.AddClaim(new Claim("picture", headimgUrl));
        }

        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, properties, Scheme.Name);
    }
}
