using System.Text.Json.Serialization;

namespace TR.Connector.Dto
{
	internal class UserData
	{
        [JsonRequired, JsonPropertyName("lastName")]
        public required string FirstName { get; set; }

        [JsonRequired, JsonPropertyName("middleName")]
        public required string MiddleName { get; set; }
        

        [JsonRequired, JsonPropertyName("lastName")]
		public required string LastName { get; set; }
        
        [JsonRequired, JsonPropertyName("telephoneNumber")]
        public required string PhoneNumber { get; set; }

        [JsonRequired, JsonPropertyName("isLead")]
        public required bool IsLead { get; set; }

        [JsonRequired, JsonPropertyName("login")]
        public required string Login { get; set; }

        [JsonRequired, JsonPropertyName("status")]
        public required string Status { get; set; }
	}
}
