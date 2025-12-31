using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Connector
{
	internal static class StringUtils
	{
		public static (string, string) SplitOnFirst(this string thisString, char character) {
			int index = thisString.IndexOf(character);
			return index == -1 ?
				   throw new ArgumentException($"'{character}' not found in target string", nameof(character)) :
				   (thisString.Substring(0, index), thisString.Substring(index + 1));
		}
	}
}
