// -------------------------------------------------------------------------------------------------
// <copyright file="SpecElementWithAttributesExtensions.cs" company="RHEA System S.A.">
//
//   Copyright 2021 RHEA System S.A.
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

namespace ReqifViewer.Infrastructure.ReqIFExtensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Components;

    using ReqIFSharp;
    using ReqifViewer.Infrastructure.Services;

    /// <summary>
    /// The <see cref="SpecElementWithAttributesExtensions"/> class provides a number of extension methods for the <see cref="SpecElementWithAttributes"/> class
    /// </summary>
    public static class SpecElementWithAttributesExtensions
    {
        /// <summary>
        /// Extracts the name (value to be displayed to the user) of the <see cref="SpecElementWithAttributes"/>
        /// </summary>
        /// <param name="specElementWithAttributes">
        /// The subject <see cref="SpecElementWithAttributes"/>
        /// </param>
        /// <returns>
        /// an object that represents the <see cref="specElementWithAttributes"/> name (value to be displayed to the user)
        /// </returns>
        /// <remarks>
        /// 1. The <see cref="Identifiable.LongName"/> is returned in case it is not empty or null
        /// 2. The first <see cref="AttributeValue"/> is returned in case it is not null (order matters, results may be unexpected and are determined on order in the original ReqIF document)
        /// 3. The <see cref="Identifiable.Identifier"/> is returned in case the <see cref="Identifiable.LongName"/> is empty or null and no <see cref="AttributeValue"/>s are present
        /// </remarks>
        public static object ExtractDisplayName(this SpecElementWithAttributes specElementWithAttributes)
        {
            if (string.IsNullOrEmpty(specElementWithAttributes.LongName))
            {
                var attributeValue = specElementWithAttributes.Values.FirstOrDefault();
                if (attributeValue != null)
                {
                    if (attributeValue is AttributeValueXHTML attributeValueXhtml)
                    {
                        var markupString = new MarkupString(attributeValueXhtml.TheValue);
                        return markupString;
                    }
                    else
                    {
                        return attributeValue.ObjectValue.ToString();
                    }
                }
                else
                {
                    return specElementWithAttributes.Identifier;
                }
            }
            else
            {
                return specElementWithAttributes.LongName;
            }
        }

        /// <summary>
        /// Queries the <see cref="ExternalObject"/> from a <see cref="SpecElementWithAttributes"/> 
        /// </summary>
        /// <param name="specElementWithAttributes">
        /// The <see cref="SpecElementWithAttributes"/> to query the <see cref="ExternalObject"/>s from
        /// </param>
        /// <returns>
        /// an <see cref="IEnumerable{ExternalObject}"/>
        /// </returns>
        public static IEnumerable<ExternalObject> QueryExternalObjects(this SpecElementWithAttributes specElementWithAttributes)
        {
            var result = new List<ExternalObject>();

            foreach (var attributeValue in specElementWithAttributes.Values.OfType<AttributeValueXHTML>())
            {
                result.AddRange(attributeValue.ExternalObjects);
            }

            return result;
        }
        /// <summary>
        /// Queries the base64 payloads of the <see cref="SpecElementWithAttributes"/>
        /// </summary>
        /// <param name="specElementWithAttributes">
        /// The <see cref="SpecElementWithAttributes"/> to query the base64 payloads from from
        /// </param>
        /// <param name="reqIfLoaderService">
        /// The <see cref="IReqIFLoaderService"/> that is used to query the payload from the associated reqifz file-stream
        /// </param>
        /// <returns>
        /// an <see cref="IEnumerable{String}"/> that contains base64 encoded strings
        /// </returns>
        public static async Task<IEnumerable<Tuple<ExternalObject, string>>> QueryBase64Payloads(this SpecElementWithAttributes specElementWithAttributes, IReqIFLoaderService reqIfLoaderService)
        {
            var result = new List<Tuple<ExternalObject, string>>();

            var cts = new CancellationTokenSource();
            
            foreach (var specObjectValue in specElementWithAttributes.Values.OfType<AttributeValueXHTML>())
            {
                foreach (var externalObject in specObjectValue.ExternalObjects)
                {
                    var payload = await reqIfLoaderService.QueryData(externalObject, cts.Token);
                    result.Add(new Tuple<ExternalObject, string>(externalObject, payload));
                }
            }

            return result;
        }

        /// <summary>
        /// Creates the url of a <see cref="SpecElementWithAttributes"/>
        /// </summary>
        /// <param name="specElementWithAttributes">
        /// The <see cref="SpecElementWithAttributes"/> of which the url is to be created
        /// </param>
        /// <returns>
        /// a string that represents the web-app url
        /// </returns>
        public static string CreateUrl(this SpecElementWithAttributes specElementWithAttributes)
        {
            var url = new StringBuilder();
            url.Append($"/reqif/{specElementWithAttributes.ReqIFContent.DocumentRoot.TheHeader.Identifier}");
            
            switch (specElementWithAttributes)
            {
                case RelationGroup relationGroup:
                    url.Append($"/relationgroup/{relationGroup.Identifier}");
                    break;
                case Specification specification:
                    url.Append($"/specification/{specification.Identifier}");
                    break;
                case SpecObject specObject:
                    url.Append($"/specobject/{specObject.Identifier}");
                    break;
                case SpecRelation specRelation:
                    url.Append($"/specrelation/{specRelation.Identifier}");
                    break;
            }
            
            return url.ToString();
        }
    }
}
