using MagicEntry.Core.Models;
using System.Collections.Generic;

namespace MagicEntry.Services.Interfaces
{
    /// <summary>
    /// Интерфейс для проверки прав доступа пользователей к плагинам
    /// </summary>
    public interface IUserAccessService
    {
        /// <summary>
        /// Получает информацию о текущем пользователе из Active Directory
        /// </summary>
        AdUserInfo GetCurrentUser();

        /// <summary>
        /// Проверяет, имеет ли текущий пользователь доступ к плагину
        /// </summary>
        /// <param name="allowedDepartments">Список разрешенных направлений (например, ["АР", "КР", "ВК"])</param>
        /// <param name="allowedUsers">Список разрешенных sAMAccountName (например, ["ivanov", "petrov"])</param>
        /// <returns>True, если доступ разрешен</returns>
        bool HasAccess(List<string> allowedDepartments, List<string> allowedUsers);
    }
}
