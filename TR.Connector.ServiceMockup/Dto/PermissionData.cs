using System.Text.Json.Serialization;

namespace TR.Connector.ServiceMockup.Dto
{
	public class PermissionData
	{
		[JsonRequired, JsonPropertyName("id")]
		public required int ID { get; set; }

		[JsonRequired, JsonPropertyName("name")]
		public required string Name { get; set; }

		//TODO: Какого типа это свойство? Что оно содержит? Из спецификации не ясно, в оригинальном коде не используется
        //public object users { get; set; }
	}
}
