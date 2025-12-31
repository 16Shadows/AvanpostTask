using Microsoft.AspNetCore.Mvc;
using TR.Connector.ServiceMockup.Dto;

namespace TR.Connector.ServiceMockup.Controllers
{
	[ApiController]
	[Route("api/v1")]
	[Consumes("application/json")]
	[Produces("application/json")]
	public class AuthController : ControllerBase
	{
		[HttpPost("login")]
		public ScalarResponse<TokenData> Login([FromBody] LoginRequestBody body)
		{
			if (body.Login == "login" && body.Password == "password")
				return new ScalarResponse<TokenData>() { 
					Success = true,
					ErrorText = null,
					Data = new TokenData()
					{
						AccessToken = (DateTime.UtcNow + TimeSpan.FromSeconds(15)).ToString("s", System.Globalization.CultureInfo.InvariantCulture),
						ExpiresIn = 15000
					}
				};
			else
				return new ScalarResponse<TokenData>() {
					Success = false,
					ErrorText = "Неверный логин/пароль"
				};
		}
	}
}
