using MagicEntry.Core.Models;
using MagicEntry.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace MagicEntry.Services
{
    /// <summary>
    /// Сервис для проверки прав доступа пользователей к плагинам на основе Active Directory
    /// </summary>
    public class UserAccessService : IUserAccessService
    {
        private AdUserInfo _currentUser;
        private readonly object _lock = new object();

        /// <summary>
        /// Получает информацию о текущем пользователе из Active Directory
        /// </summary>
        public AdUserInfo GetCurrentUser()
        {
            if (_currentUser != null)
                return _currentUser;

            lock (_lock)
            {
                if (_currentUser != null)
                    return _currentUser;

                try
                {
                    // Получаем текущего пользователя Windows
                    string currentUserName = Environment.UserName;

                    using (var ctx = new PrincipalContext(ContextType.Domain, null, "OU=Institute,DC=kgp,DC=com"))
                    using (var userPrincipal = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, currentUserName))
                    {
                        if (userPrincipal != null)
                        {
                            DirectoryEntry de = (DirectoryEntry)userPrincipal.GetUnderlyingObject();
                            string companyName = de.Properties["company"]?.Value?.ToString() ?? "";

                            // Проверяем условия из примера кода
                            if (!string.IsNullOrEmpty(companyName) &&
                                !userPrincipal.DisplayName.Contains("Краснодаргражданпроект") &&
                                !companyName.Contains("Краснодаргражданпроект"))
                            {
                                var dep = de.Properties["department"]?.Value?.ToString() ?? "";

                                _currentUser = new AdUserInfo
                                {
                                    FullName = userPrincipal.DisplayName ?? "",
                                    Email = userPrincipal.EmailAddress ?? "",
                                    Phone = de.Properties["telephoneNumber"]?.Value?.ToString() ?? "",
                                    Position = de.Properties["title"]?.Value?.ToString() ?? "",
                                    Login = userPrincipal.SamAccountName,
                                    Company = companyName,
                                    WorkGroup = dep.Contains("_") ? ParseWorkGroup(dep) : null,
                                    Department = dep
                                };

                                return _currentUser;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // В случае ошибки возвращаем пользователя с базовыми данными
                    _currentUser = new AdUserInfo
                    {
                        Login = Environment.UserName,
                        Department = "",
                        FullName = Environment.UserName
                    };
                }

                return _currentUser;
            }
        }

        private int? ParseWorkGroup(string department)
        {
            try
            {
                int underscoreIndex = department.IndexOf("_");
                if (underscoreIndex >= 0 && underscoreIndex < department.Length - 1)
                {
                    string numberPart = department.Substring(underscoreIndex + 1);
                    if (int.TryParse(numberPart, out int workGroup))
                        return workGroup;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Проверяет, имеет ли текущий пользователь доступ к плагину
        /// </summary>
        /// <param name="allowedDepartments">Список разрешенных направлений (например, ["АР", "КР", "ВК"])</param>
        /// <param name="allowedUsers">Список разрешенных sAMAccountName (например, ["ivanov", "petrov"])</param>
        /// <returns>True, если доступ разрешен</returns>
        public bool HasAccess(List<string> allowedDepartments, List<string> allowedUsers)
        {
            // Если оба списка пусты - доступ разрешен всем
            if ((allowedDepartments == null || !allowedDepartments.Any()) &&
                (allowedUsers == null || !allowedUsers.Any()))
                return true;

            var currentUser = GetCurrentUser();
            if (currentUser == null)
                return false;

            // Проверка по sAMAccountName
            if (allowedUsers != null && allowedUsers.Any())
            {
                var userList = allowedUsers.Select(u => u.Trim().ToLower()).ToList();

                if (userList.Contains(currentUser.Login.ToLower()))
                    return true;
            }

            // Проверка по направлению (department)
            if (allowedDepartments != null && allowedDepartments.Any() &&
                !string.IsNullOrWhiteSpace(currentUser.Department))
            {
                var departmentList = allowedDepartments.Select(d => d.Trim().ToUpper()).ToList();

                // Проверяем, содержит ли department пользователя какое-либо из разрешенных направлений
                foreach (var allowedDept in departmentList)
                {
                    if (currentUser.Department.ToUpper().Contains(allowedDept))
                        return true;
                }
            }

            return false;
        }
    }
}
