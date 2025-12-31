using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace TR.Connector.ServiceMockup.Authentication
{
    public class DummyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public DummyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) {}

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorizationHeader = Context.Request.Headers.Authorization;
            if (authorizationHeader.Count != 1)
                return Task.FromResult(AuthenticateResult.NoResult());

            var value = authorizationHeader.ToString();
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("Bearer "))
                return Task.FromResult(AuthenticateResult.Fail("Invalid token."));

            value = value.Substring("Bearer ".Length);
            
            if (string.IsNullOrWhiteSpace(value) || !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime tokenValue) ||
                tokenValue < DateTime.UtcNow)
                return Task.FromResult(AuthenticateResult.Fail("Invalid token."));

            var claimsIdentity = new ClaimsIdentity([], Scheme.Name);

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), new AuthenticationProperties(), Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
