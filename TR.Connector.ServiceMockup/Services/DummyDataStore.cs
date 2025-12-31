using TR.Connector.ServiceMockup.Dto;

namespace TR.Connector.ServiceMockup.Services
{
	public class DummyDataStore
	{
		public class StoredUserData : CreateUserData
		{
			public static StoredUserData Create(CreateUserData data)
			{
				return new StoredUserData()
				{
					Status = data.Status,
					PhoneNumber = data.PhoneNumber,
					Login = data.Login,
					FirstName = data.FirstName,
					LastName = data.LastName,
					MiddleName = data.MiddleName,
					IsLead = data.IsLead,
					Password = data.Password
				};
			}

			public HashSet<int> Roles { get; } = new();
			public HashSet<int> Permissions { get; } = new();
		}

		public Dictionary<string, StoredUserData> UserData { get; } = new();
		public Dictionary<int, RoleData> RoleData { get; } = new();
		public Dictionary<int, PermissionData> PermissionData { get; } = new();

		public DummyDataStore()
		{
			RoleData.Add(9, new RoleData() { ID = 9, Name = "ITRole9", CorporatePhoneNumber = "1234"});
			RoleData.Add(5, new RoleData() { ID = 5, Name = "ITRole5", CorporatePhoneNumber = "1234"});

			PermissionData.Add(5, new PermissionData() { ID = 5, Name = "RequestRight5" });

			UserData.Add("Login3", new StoredUserData()
			{
				FirstName = "FirstName3",
				MiddleName = "2",
				LastName = "3",
				IsLead = true,
				Login = "Login3",
				Password = "dummy",
				Permissions = { 5 },
				Roles = { 5 },
				PhoneNumber = "TelephoneNumber3",
				Status = "Unlock"
			});

			UserData.Add("Login7", new StoredUserData()
			{
				FirstName = "1",
				MiddleName = "2",
				LastName = "3",
				IsLead = true,
				Login = "Login7",
				Password = "dummy",
				PhoneNumber = "1234567890",
				Status = "Unlock"
			});
		}
	}
}
