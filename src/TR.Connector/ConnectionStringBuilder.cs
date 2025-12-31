using System.Text;

namespace TR.Connector
{
	/// <summary>
	/// Класс для парсинга/формирования строки подключения в формате "key1=value;key2=value".
	/// Ключи не чувствительны к регистру.
	/// </summary>
	public class ConnectionStringBuilder
	{
		public string Login
		{
			get => GetValue("login");
			set => SetValue("login", value);
		}

		public string Password
		{
			get => GetValue("password");
			set => SetValue("password", value);
		}

		public string Url
		{
			get => GetValue("url");
			set => SetValue("url", value);
		}


		protected Dictionary<string, string> _Arguments = new();

		/// <summary>
		/// Создать <see cref="ConnectionStringBuilder"/> без значений.
		/// </summary>
		public ConnectionStringBuilder() { }

		/// <summary>
		/// Создать <see cref="ConnectionStringBuilder"/> с предзаполненными значениями из строки подключения.
		/// Ключи не чувствительны к регистру.
		/// </summary>
		/// <param name="connectionString">Строка подключения</param>
		/// <exception cref="ArgumentException">
		/// 1. В строке подключения встречается строка не в формате "key=value".
		/// 2. В строке подключения встречается пустой ключ.
		/// 3. В строке подключения дублируется ключ.
		/// </exception>
		public ConnectionStringBuilder(string connectionString)
		{
			int pairNum = 0;
			foreach (string keyValuePair in connectionString.Split(';'))
			{
				pairNum++;
				(string, string) kvp;
				try
				{
					 kvp = keyValuePair.SplitOnFirst('=');
				}
				catch (ArgumentException)
				{
					throw new ArgumentException($"Invalid connection string: missing '=' in argument {pairNum}", nameof(connectionString));
				}

				kvp.Item1 = kvp.Item1.ToLower().Trim();
				
				if (kvp.Item1.Length == 0)
					throw new ArgumentException($"Invalid connection string: empty key name in argument {pairNum}", nameof(connectionString));

				if (_Arguments.ContainsKey(kvp.Item1))
					throw new ArgumentException($"Invalid connection string: duplicate key name {kvp.Item1}", nameof(connectionString));

				_Arguments.Add(kvp.Item1, kvp.Item2);
			}
		}

		/// <summary>
		/// Задать значение.
		/// </summary>
		/// <param name="key">Ключ. Не чувствителен к регистру.</param>
		/// <param name="value">Значение ключа.</param>
		/// <exception cref="ArgumentNullException">
		/// 1. key имеет значение null.
		/// 2. value имеет значение null.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// 1. key - пустая строка.
		/// </exception>
		public void SetValue(string key, string value)
		{
			key = key.ToLower().Trim();
			ArgumentNullException.ThrowIfNullOrEmpty(key);
			ArgumentNullException.ThrowIfNull(value);
			_Arguments.Add(key, value);
		}

		/// <summary>
		/// Получить значение ключа.
		/// </summary>
		/// <param name="key">Ключ. Не чувствителен к регистру.</param>
		/// <returns>Значение ключа.</returns>
		/// <exception cref="KeyNotFoundException">
		/// Ключ отсутствует в строке подключения.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// 1. key имеет значение null.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// 1. key - пустая строка.
		/// </exception>
		public string GetValue(string key)
		{
			key = key.ToLower().Trim();
			ArgumentNullException.ThrowIfNullOrEmpty(key);
			return _Arguments.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);
		}

		/// <summary>
		/// Получить значение ключа или значение по-умолчанию.
		/// </summary>
		/// <param name="key">Ключ. Не чувствителен к регистру.</param>
		/// <param name="defaultValue">Значение по умолчанию.</param>
		/// <returns>Значение ключа или значение по-умолчанию.</returns>
		/// <exception cref="ArgumentNullException">
		/// 1. key имеет значение null.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// 1. key - пустая строка.
		/// </exception>
		public string GetValueOrDefault(string key, string defaultValue)
		{
			key = key.ToLower().Trim();
			ArgumentNullException.ThrowIfNullOrEmpty(key);
			return _Arguments.GetValueOrDefault(key, defaultValue);
		}

		/// <summary>
		/// Создать строку подключения в формате "key1=value;key2=value".
		/// </summary>
		/// <returns>Строка подключения.</returns>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(_Arguments.Sum(v => v.Key.Length + v.Value.Length + 2));
			foreach (var kvp in _Arguments) {
				sb.Append(kvp.Key);
				sb.Append('=');
				sb.Append(kvp.Value);
				sb.Append(';');
			}

			//Если строка не пустая, то убираем последнюю ';', чтобы формат был корректным.
			if (sb.Length > 0)
				sb.Remove(sb.Length - 1, 1);

			return sb.ToString();
		}
	}
}
