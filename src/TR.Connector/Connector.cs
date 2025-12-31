using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Web;
using TR.Connector.Dto;
using TR.Connectors.Api.Entities;
using TR.Connectors.Api.Interfaces;

using LoginResponseBody = TR.Connector.Dto.ScalarResponse<TR.Connector.Dto.TokenData>;
using PermissionsResponseBody = TR.Connector.Dto.CollectionResponse<TR.Connector.Dto.PermissionData>;
using RolesResponseBody = TR.Connector.Dto.CollectionResponse<TR.Connector.Dto.RoleData>;
using UserResponseBody = TR.Connector.Dto.ScalarResponse<TR.Connector.Dto.UserData>;

namespace TR.Connector
{
	/// <summary>
	/// Реализация <see cref="IConnector"/> с использованием WebAPI.
	/// Не является потокобезопасной!
	/// </summary>
	public sealed class Connector : IConnector, IDisposable
	{
		private delegate HttpResponseMessage RequestRunner(HttpClient client);

		private class ReflectedProperty
		{
			public delegate object GetterType(UserData data);

			public string Name { get; }

			public GetterType Getter { get; }

			public ReflectedProperty(string name, GetterType getter)
			{
				Name = name ?? throw new ArgumentNullException(nameof(name));
				Getter = getter ?? throw new ArgumentNullException(nameof(getter));
			}
		}

		/// <summary>
		/// Набор путей для совершения запросов к API.
		/// </summary>
		private static class ApiPaths
		{
			public const string BasePath = "api/v1/";

			public const string Login = "login";

			public const string Roles = "roles/all";
			public const string Permissions = "rights/all";

			public const string CreateUser = "users/create";
			public const string UpdateUser = "users/edit";

			public static string AddRole(string login, string roleId) => $"users/{HttpUtility.UrlEncode(login)}/add/role/{HttpUtility.UrlEncode(roleId)}";
			public static string AddPermission(string login, string permissionId) => $"users/{HttpUtility.UrlEncode(login)}/add/right/{HttpUtility.UrlEncode(permissionId)}";

			public static string RevokeRole(string login, string roleId) => $"users/{HttpUtility.UrlEncode(login)}/drop/role/{HttpUtility.UrlEncode(roleId)}";
			public static string RevokePermission(string login, string permissionId) => $"users/{HttpUtility.UrlEncode(login)}/drop/right/{HttpUtility.UrlEncode(permissionId)}";

			public static string UserData(string login) => $"users/{HttpUtility.UrlEncode(login)}";
			public static string UserRoles(string login) => $"users/{HttpUtility.UrlEncode(login)}/roles";
			public static string UserPermissions(string login) => $"users/{HttpUtility.UrlEncode(login)}/rights";

		}

		#pragma warning disable CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
		public ILogger? Logger { get; set; }
		#pragma warning restore CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).

		private bool _Initialized = false;


		private HttpClient? _HttpClient;
		private DateTime _AuthTokenExpiresAt = DateTime.UnixEpoch;
		private string _Login = string.Empty;
		private string _Password = string.Empty;

		public Connector() {}

		/// <summary>
		/// Выполнить запрос с автоматическим обновлением авторизации при необходимости.
		/// </summary>
		/// <param name="request">Функция, совершающая запрос. Функция должна быть "чистой" (pure).</param>
		/// <exception cref="InvalidOperationException">
		/// 1. Функция вызвана до вызова StartUp и инициализации http-клиента.
		/// </exception>
		private HttpResponseMessage RunRequest(RequestRunner request)
		{
			if (_HttpClient == null)
				throw new InvalidOperationException("Missing HTTPClient.");

			//TODO: Из спецификации не ясно, как сервис отвечает на просроченный токен, поэтому проверяется StatusCode

			if (DateTime.UtcNow >= _AuthTokenExpiresAt)
			{
				Logger?.Debug("Обнаружен просроченный токен при выполнении запроса.");
				RefreshToken();
				HttpResponseMessage response = request(_HttpClient);
				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					Logger?.Error("Сервер отклонил запрос с обновлённым токеном.");
					throw new ConnectorApiException("Не удалось авторизоваться в сервисе.");
				}
				return response;
			}
			else
			{
				HttpResponseMessage response = request(_HttpClient);
				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					Logger?.Error("Сервер отклонил запрос с сохранённым токеном. Возможно, токен просрочен.");
					RefreshToken();
					response = request(_HttpClient);
					if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						Logger?.Error("Сервер отклонил запрос с обновлённым токеном.");
						throw new ConnectorApiException("Не удалось авторизоваться в сервисе.");
					}
				}
				return response;
			}
		}

		/// <summary>
		/// Выполнить запрос с автоматическим обновлением авторизации при необходимости и вернуть прочитанное JSON-тело ответа.
		/// </summary>
		/// <typeparam name="T">Тип, соответствующей структуре ответа.</typeparam>
		/// <param name="request">Функция, совершающая запрос. Функция должна быть "чистой" (pure).</param>
		/// <param name="throwOnApiError">Бросать ли исключение, если в теле ответа сервер сообщил об ошибке.</param>
		/// <returns>Экземпляр <typeparamref name="T"/>, сформированные из тела ответа.</returns>
		/// <exception cref="InvalidOperationException">
		/// 1. Функция вызвана до вызова StartUp и инициализации http-клиента.
		/// </exception>
		/// <exception cref="ConnectorApiException">
		/// 1. Сервер вернул не-200ый код ответа.
		/// 2. Тело ответа не соответствует <typeparamref name="T"/>.
		/// 3. В теле ответа сервер сообщил об ошибке.
		/// </exception>
		private T RunRequest<T>(RequestRunner request, bool throwOnApiError = true) where T : ResponseBase
		{
			var response = RunRequest(request);
			if (!response.IsSuccessStatusCode)
			{
				Logger?.Error($"Ошибка запроса: {response.StatusCode} ({response.ReasonPhrase}).");
				throw new ConnectorApiException($"Ошибка запроса: {response.StatusCode} ({response.ReasonPhrase}).");
			}

			T result;
			try
			{
				result = response.Content.ReadFromJsonAsync<T>().Result ??
						 throw new Exception("пустое тело");
			}
			catch (Exception ex)
			{
				Logger?.Error($"Ошибка запроса - некорректное тело ответа: {ex.Message}.");
				throw new ConnectorApiException($"Ошибка запроса: некорректное тело ответа.", ex);
			}

			if (!result.Success)
			{
				Logger?.Error($"Ошибка запроса: {result.ErrorText ?? string.Empty}.");
				if (throwOnApiError)
					throw new ConnectorApiException($"Ошибка запроса: {result.ErrorText ?? string.Empty}");
			}

			return result;
		}

		/// <summary>
		/// Выполнить GET-запрос с автоматическим обновлением авторизации при необходимости и вернуть прочитанное JSON-тело ответа.
		/// </summary>
		/// <typeparam name="T">Тип, соответствующей структуре ответа.</typeparam>
		/// <param name="path">Путь на сервере для запроса.</param>
		/// <returns>Экземпляр <typeparamref name="T"/>, сформированные из тела ответа.</returns>
		/// <exception cref="InvalidOperationException">
		/// 1. Функция вызвана до вызова StartUp и инициализации http-клиента.
		/// </exception>
		/// <exception cref="ConnectorApiException">
		/// 1. Сервер вернул не-200ый код ответа.
		/// 2. Тело ответа не соответствует <typeparamref name="T"/>.
		/// 3. В теле ответа сервер сообщил об ошибке.
		/// </exception>
		private T GetRequest<T>(string path, bool throwOnApiError = true) where T : ResponseBase
		{
			Logger?.Debug($"Выполняем запрос: GET {path}");
			return RunRequest<T>(client => client.GetAsync(path).Result, throwOnApiError);
		}

		/// <summary>
		/// Выполнить POST-запрос с автоматическим обновлением авторизации при необходимости и вернуть прочитанное JSON-тело ответа.
		/// </summary>
		/// <typeparam name="TResult">Тип, соответствующей структуре ответа.</typeparam>
		/// <typeparam name="TBody">Тип, соответствующей структуре отправляемого запроса.</typeparam>
		/// <param name="path">Путь на сервере для запроса.</param>
		/// <param name="body">Тело запроса.</param>
		/// <returns>Экземпляр <typeparamref name="TResult"/>, сформированные из тела ответа.</returns>
		/// <exception cref="InvalidOperationException">
		/// 1. Функция вызвана до вызова StartUp и инициализации http-клиента.
		/// </exception>
		/// <exception cref="ConnectorApiException">
		/// 1. Сервер вернул не-200ый код ответа.
		/// 2. Тело ответа не соответствует <typeparamref name="TResult"/>.
		/// 3. В теле ответа сервер сообщил об ошибке.
		/// </exception>
		private TResult PostRequest<TResult, TBody>(string path, TBody body, bool throwOnApiError = true) where TResult : ResponseBase
		{
			Logger?.Debug($"Выполняем запрос: POST {path}");
			return RunRequest<TResult>(client => client.PostAsJsonAsync(path, body).Result, throwOnApiError);
		}

		/// <summary>
		/// Выполнить PUT-запрос с автоматическим обновлением авторизации при необходимости и вернуть прочитанное JSON-тело ответа.
		/// </summary>
		/// <typeparam name="TResult">Тип, соответствующей структуре ответа.</typeparam>
		/// <typeparam name="TBody">Тип, соответствующей структуре отправляемого запроса.</typeparam>
		/// <param name="path">Путь на сервере для запроса.</param>
		/// <param name="body">Тело запроса.</param>
		/// <returns>Экземпляр <typeparamref name="TResult"/>, сформированные из тела ответа.</returns>
		/// <exception cref="InvalidOperationException">
		/// 1. Функция вызвана до вызова StartUp и инициализации http-клиента.
		/// </exception>
		/// <exception cref="ConnectorApiException">
		/// 1. Сервер вернул не-200ый код ответа.
		/// 2. Тело ответа не соответствует <typeparamref name="TResult"/>.
		/// 3. В теле ответа сервер сообщил об ошибке.
		/// </exception>
		private TResult PutRequest<TResult, TBody>(string path, TBody body, bool throwOnApiError = true) where TResult : ResponseBase
		{
			Logger?.Debug($"Выполняем запрос: PUT {path}");
			return RunRequest<TResult>(client => client.PutAsJsonAsync(path, body).Result, throwOnApiError);
		}

		/// <summary>
		/// Выполнить DELETE-запрос с автоматическим обновлением авторизации при необходимости и вернуть прочитанное JSON-тело ответа.
		/// </summary>
		/// <typeparam name="T">Тип, соответствующей структуре ответа.</typeparam>
		/// <param name="path">Путь на сервере для запроса.</param>
		/// <returns>Экземпляр <typeparamref name="T"/>, сформированные из тела ответа.</returns>
		/// <exception cref="InvalidOperationException">
		/// 1. Функция вызвана до вызова StartUp и инициализации http-клиента.
		/// </exception>
		/// <exception cref="ConnectorApiException">
		/// 1. Сервер вернул не-200ый код ответа.
		/// 2. Тело ответа не соответствует <typeparamref name="T"/>.
		/// 3. В теле ответа сервер сообщил об ошибке.
		/// </exception>
		private T DeleteRequest<T>(string path, bool throwOnApiError = true) where T : ResponseBase
		{
			Logger?.Debug($"Выполняем запрос: DELETE {path}");
			return RunRequest<T>(client => client.DeleteAsync(path).Result, throwOnApiError);
		}

		/// <summary>
		/// Обновить токен авторизации.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// 1. Функция вызвана до вызова StartUp и инициализации http-клиента.
		/// </exception>
		private void RefreshToken()
		{
			if (_HttpClient == null)
				throw new InvalidOperationException("Missing HTTPClient.");

			Logger?.Debug("Обновляем токен авторизации...");

			var loginBody = new LoginRequestBody(_Login, _Password);

			var loginResponse = _HttpClient.PostAsJsonAsync(ApiPaths.Login, loginBody).Result;
			if (!loginResponse.IsSuccessStatusCode)
			{
				Logger?.Error("Не удалось авторизоваться в сервисе: сервер вернул ошибку.");
				throw new ConnectorApiException("Не удалось авторизоваться в сервисе.");
			}

			var tokenResponse = loginResponse.Content.ReadFromJsonAsync<LoginResponseBody>().Result;
			if (tokenResponse == null)
			{
				Logger?.Error("Не удалось авторизоваться в сервисе: пустой ответ.");
				throw new ConnectorApiException("Не удалось авторизоваться в сервисе.");
			}

			if (!tokenResponse.Success)
			{
				Logger?.Error($"Не удалось авторизоваться в сервисе: {tokenResponse.ErrorText}.");
				throw new ConnectorApiException("Не удалось авторизоваться в сервисе.");
			}

			Logger?.Debug("Токен обновлён успешно.");

			//TODO: Из спецификации не ясно, в каких единицах expires_in, поэтому пока взял в ms.
			_AuthTokenExpiresAt = DateTime.UtcNow + TimeSpan.FromMilliseconds(tokenResponse.Data.ExpiresIn);
			_HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.Data.AccessToken);
		}

		/// <summary>
		/// Получить id права из <see cref="RoleData"/>.
		/// </summary>
		/// <param name="role">Экземпляр роли.</param>
		/// <returns>Id права</returns>
		private static string PermissionIdFromRole(RoleData role)
		{
			return $"ItRole,{role.ID}";
		}

		/// <summary>
		/// Создать экземпляр <see cref="Permission"/> из <see cref="RoleData"/>.
		/// </summary>
		/// <param name="role">Экземпляр роли.</param>
		/// <returns>Экземпляр <see cref="Permission"/></returns>
		private static Permission PermissionFromRole(RoleData role)
		{
			return new Permission(PermissionIdFromRole(role), role.Name, role.CorporatePhoneNumber);
		}

		/// <summary>
		/// Получить id права из <see cref="PermissionData"/>.
		/// </summary>
		/// <param name="permission">Экземпляр права.</param>
		/// <returns>Id права</returns>
		private static string PermissionIdFromPermission(PermissionData permission)
		{
			return $"RequestRight,{permission.ID}";
		}

		/// <summary>
		/// Создать экземпляр <see cref="Permission"/> из <see cref="PermissionData"/>.
		/// </summary>
		/// <param name="permission">Экземпляр права.</param>
		/// <returns>Экземпляр <see cref="Permission"/></returns>
		private static Permission PermissionFromPermission(PermissionData permission)
		{
			return new Permission(PermissionIdFromPermission(permission), permission.Name, string.Empty);
		}

		/// <summary>
		/// Бросить исключение, если метод StartUp не был раньше успешно вызван.
		/// </summary>
		/// <exception cref="InvalidOperationException"></exception>
		private void ThrowIfNotInitialized()
		{
			if (!_Initialized)
				throw new InvalidOperationException("This connector is not yet initialized.");
		}

		/// <summary>
		/// Бросить исключение, если метод StartUp не был раньше успешно вызван
		/// или если метод Dispose уже был вызван.
		/// </summary>
		private void ThrowIfInvalidState()
		{
			ThrowIfNotInitialized();
			ThrowIfDisposed();
		}

		#region IConnector
		//Вытягиваем свойства и их названия в json с помощью рефлексии один раз при инициализации.
		private static Dictionary<string, ReflectedProperty> _UserProperties = typeof(UserData).GetProperties()
																.Where(prop => prop.Name != nameof(UserData.Login))
																.ToDictionary(
																	prop => prop.Name,
																	prop => {
																		var dataParameter = Expression.Parameter(typeof(UserData));
																		var getter = Expression.Lambda<ReflectedProperty.GetterType>(
																			Expression.ConvertChecked(
																				Expression.Property(dataParameter, prop),
																				typeof(object)
																			),
																			dataParameter
																		).Compile();
																		return new ReflectedProperty(
																			prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name,
																			getter
																		);
																	}
																);

		public void StartUp(string connectionString)
		{
			ThrowIfDisposed();

			if (_Initialized)
				throw new InvalidOperationException("This connector is already initialized.");

			ConnectionStringBuilder conStr;
			try
			{
				conStr = new ConnectionStringBuilder(connectionString);
			}
			catch (Exception e)
			{
				Logger?.Error($"Не удалось прочитать строку подключения: {e.Message}.");
				throw;
			}

			string url;

			try
			{
				url = conStr.Url;
				_Login = conStr.Login;
				_Password = conStr.Password;
			}
			catch (KeyNotFoundException e)
			{
				Logger?.Error($"В строке подключения не найден параметр {e.Message}.");
				throw new ArgumentException($"Connection string is missing key: {e.Message}.", nameof(connectionString));
			}

			Logger?.Debug($"Коннектор будет подключаться к URL: {url}.");

			try
			{
				_HttpClient = new HttpClient();
				_HttpClient.BaseAddress = new Uri($"{url}/{ApiPaths.BasePath}");
				//Сразу проверим, удаётся ли нам авторизоваться с текущими данными, и в случае чего кинем ошибку.
				RefreshToken();
			}
			catch (Exception e)
			{
				Logger?.Error($"Не удалось запустить коннектор: {e.Message}.");
				_HttpClient!.Dispose();
				_HttpClient = null;
				_Login = string.Empty;
				_Password = string.Empty;

				throw;
			}
			
			_Initialized = true;
		}

		#region Permissions

		public void AddUserPermissions(string userLogin, IEnumerable<string> rightIds)
		{
			ThrowIfInvalidState();

			//Гарантируем, что сможем несколько раз пройтись по rightsIds
			rightIds = rightIds.ToList();

			Logger?.Debug($"Пытаемся добавить пользователю {userLogin} права: {string.Join(';', rightIds)}");

			List<string> roles = new(), permissions = new();
			foreach (string id in rightIds)
			{
				(string, string) parsedId;
				try { parsedId = id.SplitOnFirst(','); }
				catch(ArgumentException)
				{
					Logger?.Error($"Ошибка добавления прав: некорректный id {id}.");
					throw new ArgumentException($"Invalid permission id {id}.", nameof(rightIds));
				}

				switch (parsedId.Item1)
				{
					case "ItRole":
						roles.Add(parsedId.Item2);
						break;
					case "RequestRight":
						permissions.Add(parsedId.Item2);
						break;
					default:
						{
							Logger?.Error($"Ошибка добавления прав: некорректный id {id}.");
							throw new ArgumentException($"Invalid permission id {id}.", nameof(rightIds));
						}
				}
			}

			//TODO: Нужно ли проверять, что пользователь не-заблокирован?
			//Кажется, что логика предотвращения изменения прав/ролей в случае блокировки должна лежать на сервисе
			//Иначе можно добавить GET users/{login}, чтобы проверить статус.

			List<string> grantedRoles = new(), grantedPermissions = new();
			//Около-атомарно выдаём роли права и роли: в случае ошибки пытаемся откатить
			//Это, конечно, не реально атомарно, но работаем с тем API, которое есть.
			try
			{
				foreach (string role in roles)
				{
					_ = PutRequest<ResponseBase, object?>(ApiPaths.AddRole(userLogin, role), null);
					grantedRoles.Add(role);
				}
					

				foreach (string permission in permissions)
				{
					_ = PutRequest<ResponseBase, object?>(ApiPaths.AddPermission(userLogin, permission), null);
					grantedPermissions.Add(permission);
				}
			}
			catch (Exception ex)
			{
				Logger?.Error($"Ошибка добавления прав: {ex.Message}.");

				foreach (string role in grantedRoles)
					_ = DeleteRequest<ResponseBase>(ApiPaths.RevokeRole(userLogin, role));

				foreach (string permission in grantedPermissions)
					_ = DeleteRequest<ResponseBase>(ApiPaths.RevokePermission(userLogin, permission));

				throw;
			}
		}

		public void RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds)
		{
			ThrowIfInvalidState();

			//Гарантируем, что сможем несколько раз пройтись по rightsIds
			rightIds = rightIds.ToList();

			Logger?.Debug($"Пытаемся снять у пользователя {userLogin} права: {string.Join(';', rightIds)}");

			List<string> roles = new(), permissions = new();
			foreach (string id in rightIds)
			{
				(string, string) parsedId;
				try { parsedId = id.SplitOnFirst(','); }
				catch(ArgumentException)
				{
					Logger?.Error($"Ошибка снятия прав: некорректный id {id}.");
					throw new ArgumentException($"Invalid permission id {id}.", nameof(rightIds));
				}

				switch (parsedId.Item1)
				{
					case "ItRole":
						roles.Add(parsedId.Item2);
						break;
					case "RequestRight":
						permissions.Add(parsedId.Item2);
						break;
					default:
						{
							Logger?.Error($"Ошибка снятия прав: некорректный id {id}.");
							throw new ArgumentException($"Invalid permission id {id}.", nameof(rightIds));
						}
				}
			}

			//TODO: Нужно ли проверять, что пользователь не-заблокирован?
			//Кажется, что логика предотвращения изменения прав/ролей в случае блокировки должна лежать на сервисе
			//Иначе можно добавить GET users/{login}, чтобы проверить статус.

			List<string> revokedRoles = new(), revokedPermissions = new();
			//Около-атомарно выдаём роли права и роли: в случае ошибки пытаемся откатить
			//Это, конечно, не реально атомарно, но работаем с тем API, которое есть.
			try
			{
				foreach (string role in roles)
				{
					_ = DeleteRequest<ResponseBase>(ApiPaths.RevokeRole(userLogin, role));
					revokedRoles.Add(role);
				}
					

				foreach (string permission in permissions)
				{
					_ = DeleteRequest<ResponseBase>(ApiPaths.RevokePermission(userLogin, permission));
					revokedPermissions.Add(permission);
				}
			}
			catch (Exception ex)
			{
				Logger?.Error($"Ошибка снятия прав: {ex.Message}.");

				foreach (string role in revokedRoles)
					_ = PutRequest<ResponseBase, object?>(ApiPaths.AddRole(userLogin, role), null);

				foreach (string permission in revokedPermissions)
					_ = PutRequest<ResponseBase, object?>(ApiPaths.AddPermission(userLogin, permission), null);

				throw;
			}
		}

		public IEnumerable<string> GetUserPermissions(string userLogin)
		{
			ThrowIfInvalidState();
			var roles = GetRequest<RolesResponseBody>(ApiPaths.UserRoles(userLogin));
			var permissions = GetRequest<PermissionsResponseBody>(ApiPaths.UserPermissions(userLogin));

			return roles.Data.Select(PermissionIdFromRole).Concat(permissions.Data.Select(PermissionIdFromPermission));
		}
		
		public IEnumerable<Permission> GetAllPermissions()
		{
			ThrowIfInvalidState();
			var roles = GetRequest<RolesResponseBody>(ApiPaths.Roles);
			var permissions = GetRequest<PermissionsResponseBody>(ApiPaths.Permissions);

			return roles.Data.Select(PermissionFromRole).Concat(permissions.Data.Select(PermissionFromPermission));
		}
		#endregion

		#region Users
		public IEnumerable<Property> GetAllProperties()
		{
			//Т.к. Property - изменяемый тип, каждый раз генерируем новые экземпляры.
			return _UserProperties.Values.Select(prop => new Property(prop.Name, prop.Name));
		}

		public void CreateUser(UserToCreate user)
		{
			ThrowIfInvalidState();

			var props = user.Properties.ToDictionary(prop => prop.Name, prop => prop.Value);

			string GetProperty(string name) => props.GetValueOrDefault(_UserProperties[name].Name, string.Empty);

			//Можно частично заменить на рефлексию, но она медленнее будет.
			var userData = new CreateUserData()
			{
				FirstName = GetProperty(nameof(UserData.FirstName)),
				MiddleName = GetProperty(nameof(UserData.MiddleName)),
				LastName = GetProperty(nameof(UserData.LastName)),
				IsLead = bool.TryParse(GetProperty(nameof(UserData.IsLead)), out bool isLead) ? isLead : false,
				PhoneNumber = GetProperty(nameof(UserData.PhoneNumber)),
				Status = GetProperty(nameof(UserData.Status)),
				Login = user.Login,
				Password = user.HashPassword
			};

			_ = PostRequest<ResponseBase, CreateUserData>(ApiPaths.CreateUser, userData);
		}

		public bool IsUserExists(string userLogin)
		{
			ThrowIfInvalidState();
			return GetRequest<UserResponseBody>(ApiPaths.UserData(userLogin), false).Success;
		}
		public IEnumerable<UserProperty> GetUserProperties(string userLogin)
		{
			ThrowIfInvalidState();
			var user = GetRequest<UserResponseBody>(ApiPaths.UserData(userLogin)).Data;
			return _UserProperties.Values.Select(prop => new UserProperty(prop.Name, prop.Getter(user).ToString() ?? string.Empty));
		}

		public void UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin)
		{
			ThrowIfInvalidState();

			var props = properties.ToDictionary(prop => prop.Name, prop => prop.Value);
			var user = GetRequest<UserResponseBody>(ApiPaths.UserData(userLogin)).Data;

			string GetProperty(string name) => props.GetValueOrDefault(_UserProperties[name].Name, _UserProperties[name].Getter(user).ToString() ?? string.Empty);

			//Можно заменить на рефлексию, но она медленнее.
			//Можно заменить на Expression, который будет выступать в роли сеттера.
			user.FirstName = GetProperty(nameof(UserData.FirstName));
			user.MiddleName = GetProperty(nameof(UserData.MiddleName));
			user.LastName = GetProperty(nameof(UserData.LastName));
			user.IsLead = bool.TryParse(GetProperty(nameof(UserData.IsLead)), out bool isLead) ? isLead : false;
			user.PhoneNumber = GetProperty(nameof(UserData.PhoneNumber));
			user.Status = GetProperty(nameof(UserData.Status));

			_ = PutRequest<ResponseBase, UserData>(ApiPaths.UpdateUser, user);
		}
		#endregion


		#endregion

		#region IDisposable

		private void ThrowIfDisposed()
		{
			ObjectDisposedException.ThrowIf(_DisposedValue, this);
		}
		
		private bool _DisposedValue;
		private void Dispose(bool disposing)
		{
			if (!_DisposedValue)
			{
				if (disposing)
				{
					_HttpClient?.Dispose();
				}

				_DisposedValue = true;
			}
		}

		public void Dispose()
		{
			ThrowIfDisposed();
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion
	}
}
