using System.Text.Json.Serialization;

namespace TR.Connector.ServiceMockup.Dto
{
	public class CollectionResponse<TDataType> : ScalarResponse<List<TDataType>>
	{
		[JsonPropertyName("count")]
		public int Count { get; set; }
	}
}
