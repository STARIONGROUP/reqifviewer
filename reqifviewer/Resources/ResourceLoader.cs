// -------------------------------------------------------------------------------------------------
// <copyright file="ResourceLoader.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.Resources
{
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Resources;

    /// <summary>
    /// Class responsible for loading embedded resources.
    /// </summary>
    public class ResourceLoader : IResourceLoader
    {
        /// <summary>
        /// Load an embedded resource
        /// </summary>
        /// <param name="path">
        /// The path of the embedded resource
        /// </param>
        /// <returns>
        /// a string containing the contents of the embedded resource
        /// </returns>
        public string LoadEmbeddedResource(string path)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(path);

            using var reader = new StreamReader(stream ?? throw new MissingManifestResourceException());

            return reader.ReadToEnd();
        }

        /// <summary>
        /// queries the version number from the executing assembly
        /// </summary>
        /// <returns>
        /// a string representation of the version of the application
        /// </returns>
        public string QueryVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (infoVersion != null)
            {
                var plusIndex = infoVersion.InformationalVersion.IndexOf('+');

                if (plusIndex != -1)
                {
                    return infoVersion.InformationalVersion.Substring(0, plusIndex);
                }

                return infoVersion.InformationalVersion;
            }

            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var fileVersion = fileVersionInfo.FileVersion;
            return fileVersion;
        }

        /// <summary>
        /// Queries the logo with version info from the embedded resources
        /// </summary>
        /// <returns>
        /// the logo
        /// </returns>
        public string QueryLogo()
        {
            var version = this.QueryVersion();

            var logo = this.LoadEmbeddedResource("reqifviewer.Resources.ascii-art.txt")
                .Replace("reqifviewerVersion", version);

            return logo;
        }
    }
}
