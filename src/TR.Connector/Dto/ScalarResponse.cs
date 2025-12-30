using System.Text.Json.Serialization;

#nullable disable

namespace TR.Connector.Dto
{
	internal class ScalarResponse<TDataType> : ResponseBase
	{
		[JsonPropertyName("data")]
		public TDataType Data { get; set; }
	}
}
