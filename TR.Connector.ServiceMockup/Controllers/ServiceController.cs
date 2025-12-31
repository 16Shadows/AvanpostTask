using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TR.Connector.ServiceMockup.Dto;
using TR.Connector.ServiceMockup.Services;

namespace TR.Connector.ServiceMockup.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/v1")]
	[Consumes("application/json")]
	[Produces("application/json")]
	public class ServiceController : ControllerBase
	{
		private readonly DummyDataStore _DataStore;

		public ServiceController(DummyDataStore dataStore)
		{
			_DataStore = dataStore;
		}

		[HttpGet("roles/all")]
		public CollectionResponse<RoleData> GetRoles()
		{
			return new CollectionResponse<RoleData>() {
				Success = true,
				Data = _DataStore.RoleData.Values.ToList(),
				Count = _DataStore.RoleData.Count
			};
		}

		[HttpGet("rights/all")]
		public CollectionResponse<PermissionData> GetPermissions()
		{
			return new CollectionResponse<PermissionData>() {
				Success = true,
				Data = _DataStore.PermissionData.Values.ToList(),
				Count = _DataStore.PermissionData.Count
			};
		}

		[HttpPost("users/create")]
		public ResponseBase CreateUser([FromBody] CreateUserData user)
		{
			if (_DataStore.UserData.ContainsKey(user.Login))
				return new ResponseBase()
				{
					Success = false,
					ErrorText = "Пользователь уже существует"
				};

			_DataStore.UserData.Add(user.Login, DummyDataStore.StoredUserData.Create(user));

			return new ResponseBase()
			{
				Success = true
			};
		}

		[HttpPut("users/edit")]
		public ResponseBase UpdateUser([FromBody] UserData _user)
		{
			if (!_DataStore.UserData.TryGetValue(_user.Login, out DummyDataStore.StoredUserData? user) || user == null)
				return new ResponseBase()
				{
					Success = false,
					ErrorText = "Пользователь не существует"
				};

			user.Status = _user.Status;
			user.FirstName = _user.FirstName;
			user.MiddleName = _user.MiddleName;
			user.LastName = _user.LastName;
			user.PhoneNumber = _user.PhoneNumber;
			user.IsLead = _user.IsLead;

			return new ResponseBase()
			{
				Success = true
			};
		}

		[HttpGet("users/{login}")]
		public ScalarResponse<UserData> GetUser([FromRoute] string login)
		{
			if (!_DataStore.UserData.TryGetValue(login, out DummyDataStore.StoredUserData? user) || user == null)
				return new ScalarResponse<UserData>()
				{
					Success = false,
					ErrorText = "Пользователь не существует"
				};

			return new ScalarResponse<UserData>()
			{
				Success = true,
				Data = UserData.Create(user)
			};
		}

		[HttpGet("users/{login}/roles")]
		public CollectionResponse<RoleData> GetUserRoles([FromRoute] string login)
		{
			if (!_DataStore.UserData.TryGetValue(login, out DummyDataStore.StoredUserData? user) || user == null)
				return new CollectionResponse<RoleData>()
				{
					Success = false,
					ErrorText = "Пользователь не существует"
				};

			return new CollectionResponse<RoleData>()
			{
				Success = true,
				Count = user.Roles.Count,
				Data = user.Roles.Select(r => _DataStore.RoleData[r]).ToList()
			};
		}

		[HttpGet("users/{login}/rights")]
		public CollectionResponse<PermissionData> GetUserPermissions([FromRoute] string login)
		{
			if (!_DataStore.UserData.TryGetValue(login, out DummyDataStore.StoredUserData? user) || user == null)
				return new CollectionResponse<PermissionData>()
				{
					Success = false,
					ErrorText = "Пользователь не существует"
				};

			return new CollectionResponse<PermissionData>()
			{
				Success = true,
				Count = user.Permissions.Count,
				Data = user.Permissions.Select(r => _DataStore.PermissionData[r]).ToList()
			};
		}

		[HttpPut("users/{login}/add/role/{roleId}")]
		public ResponseBase AddRole([FromRoute] string login, [FromRoute] int roleId)
		{
			if (!_DataStore.UserData.TryGetValue(login, out DummyDataStore.StoredUserData? user) || user == null)
				return new ResponseBase()
				{
					Success = false,
					ErrorText = "Пользователь не существует"
				};

			bool success = user.Roles.Add(roleId);

			return new ResponseBase()
			{
				Success = success,
				ErrorText = success ? null : "Эта роль уже выдана пользователю."
			};
		}

		[HttpDelete("users/{login}/drop/role/{roleId}")]
		public ResponseBase RemoveRole([FromRoute] string login, [FromRoute] int roleId)
		{
			if (!_DataStore.UserData.TryGetValue(login, out DummyDataStore.StoredUserData? user) || user == null)
				return new ResponseBase()
				{
					Success = false,
					ErrorText = "Пользователь не существует"
				};

			bool success = user.Roles.Remove(roleId);

			return new ResponseBase()
			{
				Success = success,
				ErrorText = success ? null : "Эта роль не выдана пользователю."
			};
		}

		[HttpPut("users/{login}/add/right/{permissionId}")]
		public ResponseBase AddPermission([FromRoute] string login, [FromRoute] int permissionId)
		{
			if (!_DataStore.UserData.TryGetValue(login, out DummyDataStore.StoredUserData? user) || user == null)
				return new ResponseBase()
				{
					Success = false,
					ErrorText = "Пользователь не существует"
				};

			bool success = user.Permissions.Add(permissionId);

			return new ResponseBase()
			{
				Success = success,
				ErrorText = success ? null : "Это право уже выдано пользователю."
			};
		}

		[HttpDelete("users/{login}/drop/right/{permissionId}")]
		public ResponseBase RemovePermission([FromRoute] string login, [FromRoute] int permissionId)
		{
			if (!_DataStore.UserData.TryGetValue(login, out DummyDataStore.StoredUserData? user) || user == null)
				return new ResponseBase()
				{
					Success = false,
					ErrorText = "Пользователь не существует"
				};

			bool success = user.Permissions.Remove(permissionId);

			return new ResponseBase()
			{
				Success = success,
				ErrorText = success ? null : "Это право не выдано пользователю."
			};
		}
	}
}
