using System.Text.Json.Serialization;

namespace TR.Connector.ServiceMockup.Dto
{
	public class RoleData
	{
		[JsonRequired, JsonPropertyName("id")]
		public required int ID { get; set; }

		[JsonRequired, JsonPropertyName("name")]
		public required string Name { get; set; }

		[JsonRequired, JsonPropertyName("corporatePhoneNumber")]
		public required string CorporatePhoneNumber { get; set; }
	}
}
