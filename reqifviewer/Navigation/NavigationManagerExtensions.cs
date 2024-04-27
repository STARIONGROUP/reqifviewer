// -------------------------------------------------------------------------------------------------
// <copyright file="NavigationManagerExtensions.cs" company="Starion Group S.A.">
//
//   Copyright 2021-2024 Starion Group S.A.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//
// </copyright>
// -------------------------------------------------------------------------------------------------

namespace ReqifViewer.Navigation
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.WebUtilities;

    /// <summary>
    /// The <see cref="NavigationManagerExtensions"/> class provides a number of extension methods for the <see cref="NavigationManager"/> class
    /// </summary>
    public static class NavigationManagerExtensions
    {
        /// <summary>
        /// Try to gets the query parameter by key
        /// </summary>
        /// <typeparam name="T">
        /// The type the value
        /// </typeparam>
        /// <param name="navigationManager">
        /// The <see cref="NavigationManager"/>
        /// </param>
        /// <param name="key">
        /// The key of the query parameter
        /// </param>
        /// <param name="value">
        /// the value of the query parameter
        /// </param>
        /// <returns>
        /// True when the query parameter was found, false if not found
        /// </returns>
        public static bool TryGetQueryString<T>(this NavigationManager navigationManager, string key, out T value)
        {
            var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);

            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue(key, out var valueFromQueryString))
            {
                if (typeof(T) == typeof(int) && int.TryParse(valueFromQueryString, out var valueAsInt))
                {
                    value = (T)(object)valueAsInt;
                    return true;
                }

                if (typeof(T) == typeof(string))
                {
                    value = (T)(object)valueFromQueryString.ToString();
                    return true;
                }

                if (typeof(T) == typeof(decimal) && decimal.TryParse(valueFromQueryString, out var valueAsDecimal))
                {
                    value = (T)(object)valueAsDecimal;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
