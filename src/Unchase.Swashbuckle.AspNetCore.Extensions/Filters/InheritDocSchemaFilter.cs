﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.XPath;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Unchase.Swashbuckle.AspNetCore.Extensions.Extensions;

namespace Unchase.Swashbuckle.AspNetCore.Extensions.Filters
{
    /// <summary>
    /// Adds documentation that is provided by the &lt;inhertidoc /&gt; tag.
    /// </summary>
    /// <seealso cref="ISchemaFilter" />
    internal class InheritDocSchemaFilter : ISchemaFilter
    {
        #region Fields

        private const string SummaryTag = "summary";
        private const string RemarksTag = "remarks";
        private const string ExampleTag = "example";
        private readonly bool _includeRemarks;
        private readonly List<XPathDocument> _documents;
        private readonly Dictionary<string, string> _inheritedDocs;
        private readonly Type[] _excludedTypes;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="InheritDocSchemaFilter" /> class.
        /// </summary>
        /// <param name="inheritedDocs">Dictionary with inheritdoc in form of name-cref.</param>
        /// <param name="includeRemarks">Include remarks from inheritdoc XML comments.</param>
        /// <param name="documents">List of <see cref="XPathDocument"/>.</param>
        public InheritDocSchemaFilter(List<XPathDocument> documents, Dictionary<string, string> inheritedDocs, bool includeRemarks = false)
            : this(documents, inheritedDocs, includeRemarks, Array.Empty<Type>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InheritDocSchemaFilter" /> class.
        /// </summary>
        /// <param name="inheritedDocs">Dictionary with inheritdoc in form of name-cref.</param>
        /// <param name="includeRemarks">Include remarks from inheritdoc XML comments.</param>
        /// <param name="excludedTypes">Excluded types.</param>
        /// <param name="documents">List of <see cref="XPathDocument"/>.</param>
        public InheritDocSchemaFilter(List<XPathDocument> documents, Dictionary<string, string> inheritedDocs, bool includeRemarks = false, params Type[] excludedTypes)
        {
            _includeRemarks = includeRemarks;
            _excludedTypes = excludedTypes;
            _documents = documents;
            _inheritedDocs = inheritedDocs;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Apply filter.
        /// </summary>
        /// <param name="schema"><see cref="OpenApiSchema"/>.</param>
        /// <param name="context"><see cref="SchemaFilterContext"/>.</param>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (_excludedTypes.Any() && _excludedTypes.ToList().Contains(context.Type))
            {
                return;
            }

            // Try to apply a description for inherited types.
            string memberName = XmlCommentsNodeNameHelper.GetMemberNameForType(context.Type);
            if (string.IsNullOrEmpty(schema.Description) && _inheritedDocs.ContainsKey(memberName))
            {
                string cref = _inheritedDocs[memberName];
                var target = context.Type.GetTargetRecursive(_inheritedDocs, cref);

                var targetXmlNode = XmlCommentsExtensions.GetMemberXmlNode(XmlCommentsNodeNameHelper.GetMemberNameForType(target), _documents);
                var summaryNode = targetXmlNode?.SelectSingleNode(SummaryTag);

                if (summaryNode != null)
                {
                    schema.Description = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);

                    if (_includeRemarks)
                    {
                        var remarksNode = targetXmlNode.SelectSingleNode(RemarksTag);
                        if (remarksNode != null && !string.IsNullOrWhiteSpace(remarksNode.InnerXml))
                        {
                            schema.Description += $" ({XmlCommentsTextHelper.Humanize(remarksNode.InnerXml)})";
                        }
                    }
                }
            }

            if (schema.Properties == null)
            {
                return;
            }

            // Add the summary and examples for the properties.
            foreach (var entry in schema.Properties)
            {
                var memberInfo = ((TypeInfo)context.Type).DeclaredMembers.FirstOrDefault(p => p.Name.Equals(entry.Key, StringComparison.OrdinalIgnoreCase));
                if (memberInfo != null)
                {
                    entry.Value.ApplyPropertyComments(memberInfo, _documents, _inheritedDocs, _includeRemarks, _excludedTypes);
                }
            }
        }
        
        #endregion
    }
}
