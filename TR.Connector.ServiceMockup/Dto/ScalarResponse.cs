using System.Text.Json.Serialization;

#nullable disable

namespace TR.Connector.ServiceMockup.Dto
{
	public class ScalarResponse<TDataType> : ResponseBase
	{
		[JsonPropertyName("data")]
		public TDataType Data { get; set; }
	}
}
