// -------------------------------------------------------------------------------------------------
// <copyright file="SpecTypeExtensions.cs" company="Starion Group S.A.">
//
//   Copyright 2021-2025 Starion Group S.A.
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

namespace ReqifViewer.ReqIFExtensions
{
    using System.Text;

    using ReqIFSharp;

    /// <summary>
    /// The <see cref="SpecTypeExtensions"/> class provides a number of extension methods for the <see cref="SpecType"/> class
    /// </summary>
    public static class SpecTypeExtensions
    {
        /// <summary>
        /// Creates the url of a <see cref="SpecType"/>
        /// </summary>
        /// <param name="specType">
        /// The <see cref="SpecType"/> of which the url is to be created
        /// </param>
        /// <returns>
        /// a string that represents the web-app url
        /// </returns>
        public static string CreateUrl(this SpecType specType)
        {
            var url = new StringBuilder();
            url.Append($"/reqif/{specType.ReqIFContent.DocumentRoot.TheHeader.Identifier}");
            url.Append($"/spectype/{specType.Identifier}");
            
            return url.ToString();
        }
    }
}
