﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Configy.Containers;
using Configy.Parsing;

namespace Configy
{
	public class XmlContainerBuilder
	{
		private readonly IContainerDefinitionVariablesReplacer _variablesReplacer;

		public XmlContainerBuilder(IContainerDefinitionVariablesReplacer variablesReplacer)
		{
			_variablesReplacer = variablesReplacer;
		}

		public virtual IEnumerable<IContainer> GetContainers(IEnumerable<ContainerDefinition> definitions)
		{
			foreach (var definition in definitions)
			{
				if (definition.Abstract) continue;

				yield return GetContainer(definition);
			}
		}

		public virtual IContainer GetContainer(ContainerDefinition definition)
		{
			_variablesReplacer?.ReplaceVariables(definition);

			var container = CreateContainer(definition);
			
			foreach (XmlElement dependency in definition.Definition.ChildNodes.OfType<XmlElement>())
			{
				RegisterConfigTypeInterfaces(dependency, container);
			}

			return container;
		}

		/// <summary>
		/// Registers a dependency entry with the container, using its interface(s) as the registrations
		/// </summary>
		protected virtual void RegisterConfigTypeInterfaces(XmlElement dependency, IContainer container)
		{
			var type = GetConfigType(dependency);
			var interfaces = type.Type.GetInterfaces();
			var attributes = GetUnmappedAttributes(dependency);

			foreach (var @interface in interfaces)
			{
				RegisterConfigTypeInterface(container, @interface, type, attributes, dependency);
			}
		}

		/// <summary>
		/// Registers a specific interface to a type with the container.
		/// </summary>
		protected virtual void RegisterConfigTypeInterface(IContainer container, Type interfaceType, TypeRegistration implementationRegistration, KeyValuePair<string, object>[] unmappedAttributes, XmlElement dependency)
		{
			container.Register(interfaceType, () => container.Activate(implementationRegistration.Type, unmappedAttributes), implementationRegistration.SingleInstance);
		}

		/// <summary>
		/// Resolves an attribute of an XML element into a C# Type, using the Assembly Qualified Name
		/// </summary>
		protected virtual TypeRegistration GetConfigType(XmlElement dependency)
		{
			var typeString = GetAttributeValue(dependency, "type");

			var isSingleInstance = bool.TrueString.Equals(GetAttributeValue(dependency, "singleInstance"), StringComparison.OrdinalIgnoreCase);

			if (string.IsNullOrEmpty(typeString))
			{
				throw new InvalidOperationException($"Missing type attribute for dependency '{dependency.Name}'. Specify an assembly-qualified name for your dependency.");
			}

			var type = Type.GetType(typeString, false);

			if (type == null)
			{
				throw new InvalidOperationException($"Unable to resolve type '{typeString}' on dependency config node '{dependency.Name}'");
			}

			return new TypeRegistration { Type = type, SingleInstance = isSingleInstance };
		}

		/// <summary>
		/// Gets unmapped (i.e. not 'type') attributes or body of a dependency declaration. These are passed as possible constructor parameters to the object.
		/// </summary>
		protected virtual KeyValuePair<string, object>[] GetUnmappedAttributes(XmlElement dependency)
		{
			// ReSharper disable once PossibleNullReferenceException
			var attributes = dependency.Attributes.Cast<XmlAttribute>()
				.Where(attr => attr.Name != "type" && attr.Name != "singleInstance")
				.Select(attr =>
				{
					// mapping boolean values to bool constructor params
					bool boolean;
					if (bool.TryParse(attr.InnerText, out boolean)) return new KeyValuePair<string, object>(attr.Name, boolean);

					// mapping integer values to int constructor params
					int integer;
					if (int.TryParse(attr.InnerText, out integer)) return new KeyValuePair<string, object>(attr.Name, integer);

					return new KeyValuePair<string, object>(attr.Name, attr.InnerText);
				});

			// we pass it the XML element as 'configNode'
			attributes = attributes.Concat(new[] { new KeyValuePair<string, object>("configNode", dependency) });

			return attributes.ToArray();
		}

		/// <summary>
		/// Gets an XML attribute value, returning null if it does not exist and its inner text otherwise.
		/// </summary>
		protected virtual string GetAttributeValue(XmlNode node, string attribute)
		{
			var attributeItem = node?.Attributes?[attribute];

			return attributeItem?.InnerText;
		}

		protected virtual IContainer CreateContainer(ContainerDefinition definition)
		{
			return new MicroContainer(definition.Name, definition.Extends);
		}

		protected class TypeRegistration
		{
			public Type Type { get; set; }
			public bool SingleInstance { get; set; }
		}
	}
}
