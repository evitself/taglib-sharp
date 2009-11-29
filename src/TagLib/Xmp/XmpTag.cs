//
// XmpTag.cs:
//
// Author:
//   Ruben Vermeersch (ruben@savanne.be)
//
// Copyright (C) 2009 Ruben Vermeersch
//
// This library is free software; you can redistribute it and/or modify
// it  under the terms of the GNU Lesser General Public License version
// 2.1 as published by the Free Software Foundation.
//
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307
// USA
//

using System;
using System.Collections.Generic;
using System.Xml;

using TagLib.Image;

namespace TagLib.Xmp
{
	/// <summary>
	///    Holds XMP (Extensible Metadata Platform) metadata.
	/// </summary>
	public class XmpTag : ImageTag
	{
#region Parsing speedup
		private Dictionary<string, Dictionary<string, XmpNode>> nodes;

		/// <summary>
		///    Adobe namespace
		/// </summary>
		public static readonly string ADOBE_X_NS = "adobe:ns:meta/";

		/// <summary>
		///    Camera Raw Settings namespace
		/// </summary>
		public static readonly string CRS_NS = "http://ns.adobe.com/camera-raw-settings/1.0/";

		/// <summary>
		///    Dublin Core namespace
		/// </summary>
		public static readonly string DC_NS = "http://purl.org/dc/elements/1.1/";

		/// <summary>
		///    Exif namespace
		/// </summary>
		public static readonly string EXIF_NS = "http://ns.adobe.com/exif/1.0/";

		/// <summary>
		///    RDF namespace
		/// </summary>
		public static readonly string RDF_NS = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

		/// <summary>
		///    STDIM namespace
		/// </summary>
		public static readonly string STDIM_NS = "http://ns.adobe.com/xap/1.0/sType/Dimensions#";

		/// <summary>
		///    TIFF namespace
		/// </summary>
		public static readonly string TIFF_NS = "http://ns.adobe.com/tiff/1.0/";

		/// <summary>
		///    XAP (XMP's previous name) namespace
		/// </summary>
		public static readonly string XAP_NS = "http://ns.adobe.com/xap/1.0/";

		/// <summary>
		///    XML namespace
		/// </summary>
		public static readonly string XML_NS = "http://www.w3.org/XML/1998/namespace";

		/// <summary>
		///    XMLNS namespace
		/// </summary>
		public static readonly string XMLNS_NS = "http://www.w3.org/2000/xmlns/";

		/// <summary>
		///    XMP TPg (XMP Paged-Text) namespace
		/// </summary>
		public static readonly string XMPTG_NS = "http://ns.adobe.com/xap/1.0/t/pg/";

		internal static readonly string ABOUT_URI = "about";
		internal static readonly string ABOUT_EACH_URI = "aboutEach";
		internal static readonly string ABOUT_EACH_PREFIX_URI = "aboutEachPrefix";
		internal static readonly string ALT_URI = "Alt";
		internal static readonly string BAG_URI = "Bag";
		internal static readonly string BAG_ID_URI = "bagID";
		internal static readonly string DATA_TYPE_URI = "datatype";
		internal static readonly string DESCRIPTION_URI = "Description";
		internal static readonly string ID_URI = "ID";
		internal static readonly string LANG_URI = "lang";
		internal static readonly string LI_URI = "li";
		internal static readonly string NODE_ID_URI = "nodeID";
		internal static readonly string PARSE_TYPE_URI = "parseType";
		internal static readonly string RDF_URI = "RDF";
		internal static readonly string RESOURCE_URI = "resource";
		internal static readonly string SEQ_URI = "Seq";
		internal static readonly string VALUE_URI = "value";

		static readonly NameTable NameTable;

		static XmpTag () {
			// This allows for fast string comparison using operator==
			NameTable = new NameTable ();
			// Namespaces
			AddNamespacePrefix ("x", ADOBE_X_NS);
			AddNamespacePrefix ("crs", CRS_NS);
			AddNamespacePrefix ("dc", DC_NS);
			AddNamespacePrefix ("exif", EXIF_NS);
			AddNamespacePrefix ("rdf", RDF_NS);
			AddNamespacePrefix ("stDim", STDIM_NS);
			AddNamespacePrefix ("tiff", TIFF_NS);
			AddNamespacePrefix ("xmp", XAP_NS);
			AddNamespacePrefix ("xml", XML_NS);
			AddNamespacePrefix ("xmlns", XMLNS_NS);
			AddNamespacePrefix ("xmpTPg", XMPTG_NS);

			// Attribute names
			NameTable.Add (ABOUT_URI);
			NameTable.Add (ABOUT_EACH_URI);
			NameTable.Add (ABOUT_EACH_PREFIX_URI);
			NameTable.Add (ALT_URI);
			NameTable.Add (BAG_URI);
			NameTable.Add (BAG_ID_URI);
			NameTable.Add (DATA_TYPE_URI);
			NameTable.Add (DESCRIPTION_URI);
			NameTable.Add (ID_URI);
			NameTable.Add (LANG_URI);
			NameTable.Add (LI_URI);
			NameTable.Add (NODE_ID_URI);
			NameTable.Add (PARSE_TYPE_URI);
			NameTable.Add (RDF_URI);
			NameTable.Add (RESOURCE_URI);
			NameTable.Add (SEQ_URI);
			NameTable.Add (VALUE_URI);
		}

		private static Dictionary<string, string> NamespacePrefixes = new Dictionary<string, string>();

		private static int anon_ns_count = 0;

		static void AddNamespacePrefix (string prefix, string ns)
		{
			NameTable.Add (ns);
			NamespacePrefixes.Add (ns, prefix);
		}

#endregion

#region Constructors

		public XmpTag (string data)
		{
			XmlDocument doc = new XmlDocument (NameTable);
			doc.LoadXml (data);

			XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
			nsmgr.AddNamespace("x", ADOBE_X_NS);
			nsmgr.AddNamespace("rdf", RDF_NS);

			XmlNode node = doc.SelectSingleNode("/x:xmpmeta/rdf:RDF", nsmgr);
			// Old versions of XMP were called XAP, fall back to this case (tested in sample_xap.jpg)
			node = node ?? doc.SelectSingleNode("/x:xapmeta/rdf:RDF", nsmgr);
			if (node == null)
				throw new CorruptFileException ();

			NodeTree = ParseRDF (node);
			NodeTree.Accept (new NodeIndexVisitor (this));
			//NodeTree.Dump ();
			//Console.WriteLine (node.OuterXml);
		}

#endregion

#region Private Methods

		// 7.2.9 RDF
		//		start-element ( URI == rdf:RDF, attributes == set() )
		//		nodeElementList
		//		end-element()
		private XmpNode ParseRDF (XmlNode rdf_node)
		{
			XmpNode top = new XmpNode (String.Empty, String.Empty);
			foreach (XmlNode node in rdf_node.ChildNodes) {
				if (node is XmlWhitespace)
					continue;

				if (node.Is (RDF_NS, DESCRIPTION_URI)) {
					var attr = node.Attributes.GetNamedItem (RDF_NS, ABOUT_URI) as XmlAttribute;
					if (attr != null) {
						if (top.Name != String.Empty && top.Name != attr.InnerText)
							throw new CorruptFileException ("Multiple inconsistent rdf:about values!");
						top.Name = attr.InnerText;
					}
					continue;
				}

				throw new CorruptFileException ("Cannot have anything other than rdf:Description at the top level");
			}
			ParseNodeElementList (top, rdf_node);
			return top;
		}

		// 7.2.10 nodeElementList
		//		ws* ( nodeElement ws* )*
		private void ParseNodeElementList (XmpNode parent, XmlNode xml_parent)
		{
			foreach (XmlNode node in xml_parent.ChildNodes) {
				if (node is XmlWhitespace)
					continue;
				ParseNodeElement (parent, node);
			}
		}

		// 7.2.11 nodeElement
		//		start-element ( URI == nodeElementURIs,
		//						attributes == set ( ( idAttr | nodeIdAttr | aboutAttr )?, propertyAttr* ) )
		//		propertyEltList
		//		end-element()
		//
		// 7.2.13 propertyEltList
		//		ws* ( propertyElt ws* )*
		private void ParseNodeElement (XmpNode parent, XmlNode node)
		{
			if (!node.IsNodeElement ())
				throw new CorruptFileException ("Unexpected node found, invalid RDF?");

			if (node.Is (RDF_NS, SEQ_URI)) {
				parent.Type = XmpNodeType.Seq;
			} else if (node.Is (RDF_NS, ALT_URI)) {
				parent.Type = XmpNodeType.Alt;
			} else if (node.Is (RDF_NS, BAG_URI)) {
				parent.Type = XmpNodeType.Bag;
			} else if (node.Is (RDF_NS, DESCRIPTION_URI)) {
				parent.Type = XmpNodeType.Struct;
			} else {
				throw new Exception ("Unknown nodeelement found! Perhaps an unimplemented collection?");
			}

			foreach (XmlAttribute attr in node.Attributes) {
				if (attr.In (XMLNS_NS))
					continue;
				if (attr.Is (RDF_NS, ID_URI) || attr.Is (RDF_NS, NODE_ID_URI) || attr.Is (RDF_NS, ABOUT_URI))
					continue;
				if (attr.Is (XML_NS, LANG_URI))
					throw new CorruptFileException ("xml:lang is not allowed here!");
				parent.AddChild (new XmpNode (attr.NamespaceURI, attr.LocalName, attr.InnerText));
			}

			foreach (XmlNode child in node.ChildNodes) {
				if (child is XmlWhitespace || child is XmlComment)
					continue;
				ParsePropertyElement (parent, child);
			}
		}

		// 7.2.14 propertyElt
		//		resourcePropertyElt | literalPropertyElt | parseTypeLiteralPropertyElt |
		//		parseTypeResourcePropertyElt | parseTypeCollectionPropertyElt |
		//		parseTypeOtherPropertyElt | emptyPropertyElt
		private void ParsePropertyElement (XmpNode parent, XmlNode node)
		{
			int count = 0;
			bool has_other = false;
			foreach (XmlAttribute attr in node.Attributes) {
				if (!attr.In (XMLNS_NS))
					count++;

				if (!attr.Is (XML_NS, LANG_URI) && !attr.Is (RDF_NS, ID_URI) && !attr.In (XMLNS_NS))
					has_other = true;
			}

			if (count > 3) {
				ParseEmptyPropertyElement (parent, node);
			} else {
				if (!has_other) {
					if (!node.HasChildNodes) {
						ParseEmptyPropertyElement (parent, node);
					} else {
						bool only_text = true;
						foreach (XmlNode child in node.ChildNodes) {
							if (!(child is XmlText))
								only_text = false;
						}

						if (only_text) {
							ParseLiteralPropertyElement (parent, node);
						} else {
							ParseResourcePropertyElement (parent, node);
						}
					}
				} else {
					foreach (XmlAttribute attr in node.Attributes) {
						if (attr.Is (XML_NS, LANG_URI) || attr.Is (RDF_NS, ID_URI) || attr.In (XMLNS_NS))
							continue;

						if (attr.Is (RDF_NS, DATA_TYPE_URI)) {
							ParseLiteralPropertyElement (parent, node);
						} else if (!attr.Is (RDF_NS, PARSE_TYPE_URI)) {
							ParseEmptyPropertyElement (parent, node);
						} else if (attr.InnerText.Equals ("Resource")) {
							ParseTypeResourcePropertyElement (parent, node);
						} else {
							// Neither Literal, Collection or anything else is allowed
							throw new CorruptFileException (String.Format ("This is not allowed in XMP! Bad XMP: {0}", node.OuterXml));
						}
					}
				}
			}
		}

		// 7.2.15 resourcePropertyElt
		//		start-element ( URI == propertyElementURIs, attributes == set ( idAttr? ) )
		//		ws* nodeElement ws*
		//		end-element()
		private void ParseResourcePropertyElement (XmpNode parent, XmlNode node)
		{
			if (!node.IsPropertyElement ())
				throw new CorruptFileException ("Invalid property");

			XmpNode new_node = new XmpNode (node.NamespaceURI, node.LocalName);
			foreach (XmlAttribute attr in node.Attributes) {
				if (attr.Is (XML_NS, LANG_URI)) {
					new_node.AddQualifier (new XmpNode (XML_NS, LANG_URI, attr.InnerText));
				} else if (attr.Is (RDF_NS, ID_URI) || attr.In (XMLNS_NS)) {
					continue;
				}

				throw new CorruptFileException (String.Format ("Invalid attribute: {0}", attr.OuterXml));
			}

			bool has_xml_children = false;
			foreach (XmlNode child in node.ChildNodes) {
				if (child is XmlWhitespace)
					continue;
				if (child is XmlText)
					throw new CorruptFileException ("Can't have text here!");
				has_xml_children = true;

				ParseNodeElement (new_node, child);
			}

			if (!has_xml_children)
				throw new CorruptFileException ("Missing children for resource property element");

			parent.AddChild (new_node);
		}

		// 7.2.16 literalPropertyElt
		//		start-element ( URI == propertyElementURIs, attributes == set ( idAttr?, datatypeAttr?) )
		//		text()
		//		end-element()
		private void ParseLiteralPropertyElement (XmpNode parent, XmlNode node)
		{
			if (!node.IsPropertyElement ())
				throw new CorruptFileException ("Invalid property");
			parent.AddChild (CreateTextPropertyWithQualifiers (node, node.InnerText));
		}

		// 7.2.18 parseTypeResourcePropertyElt
		//		start-element ( URI == propertyElementURIs, attributes == set ( idAttr?, parseResource ) )
		//		propertyEltList
		//		end-element()
		private void ParseTypeResourcePropertyElement (XmpNode parent, XmlNode node)
		{
			if (!node.IsPropertyElement ())
				throw new CorruptFileException ("Invalid property");

			XmpNode new_node = new XmpNode (node.NamespaceURI, node.LocalName);
			new_node.Type = XmpNodeType.Struct;

			foreach (XmlNode attr in node.Attributes) {
				if (attr.Is (XML_NS, LANG_URI))
					new_node.AddQualifier (new XmpNode (XML_NS, LANG_URI, attr.InnerText));
			}

			foreach (XmlNode child in node.ChildNodes) {
				if (child is XmlWhitespace || child is XmlComment)
					continue;
				ParsePropertyElement (new_node, child);
			}

			parent.AddChild (new_node);
		}

		// 7.2.21 emptyPropertyElt
		//		start-element ( URI == propertyElementURIs,
		//						attributes == set ( idAttr?, ( resourceAttr | nodeIdAttr )?, propertyAttr* ) )
		//		end-element()
		private void ParseEmptyPropertyElement (XmpNode parent, XmlNode node)
		{
			if (!node.IsPropertyElement ())
				throw new CorruptFileException ("Invalid property");
			if (node.HasChildNodes)
				throw new CorruptFileException (String.Format ("Can't have content in this node! Node: {0}", node.OuterXml));

			var rdf_value = node.Attributes.GetNamedItem (VALUE_URI, RDF_NS) as XmlAttribute;
			var rdf_resource = node.Attributes.GetNamedItem (RESOURCE_URI, RDF_NS) as XmlAttribute;

			// Options 1 and 2
			var simple_prop_val = rdf_value ?? rdf_resource ?? null;
			if (simple_prop_val != null) {
				string value = simple_prop_val.InnerText;
				node.Attributes.Remove (simple_prop_val); // This one isn't a qualifier.
				parent.AddChild (CreateTextPropertyWithQualifiers (node, value));
				return;
			}

			// Options 3 & 4
			var new_node = new XmpNode (node.NamespaceURI, node.LocalName);
			foreach (XmlAttribute a in node.Attributes) {
				if (a.Is(RDF_NS, ID_URI) || a.Is(RDF_NS, NODE_ID_URI)) {
					continue;
				} else if (a.In (XMLNS_NS)) {
					continue;
				} else if (a.Is (XML_NS, LANG_URI)) {
					new_node.AddQualifier (new XmpNode (XML_NS, LANG_URI, a.InnerText));
				}

				new_node.AddChild (new XmpNode (a.NamespaceURI, a.LocalName, a.InnerText));
			}
			parent.AddChild (new_node);
		}

		private XmpNode CreateTextPropertyWithQualifiers (XmlNode node, string value)
		{
			XmpNode t = new XmpNode (node.NamespaceURI, node.LocalName, value);
			foreach (XmlAttribute attr in node.Attributes) {
				if (attr.In (XMLNS_NS))
					continue;
				t.AddQualifier (new XmpNode (attr.NamespaceURI, attr.LocalName, attr.InnerText));
			}
			return t;
		}

#endregion

#region Public Properties

		/// <summary>
		///    Gets the tag types contained in the current instance.
		/// </summary>
		/// <value>
		///    Always <see cref="TagTypes.XMP" />.
		/// </value>
		public override TagTypes TagTypes {
			get {return TagTypes.XMP;}
		}

		/// <summary>
		///    Get the tree of <see cref="XmpNode" /> nodes. These contain the values
		///    parsed from the XMP file.
		/// </summary>
		public XmpNode NodeTree {
			get; private set;
		}

#endregion

#region Public Methods

		/// <summary>
		///    Clears the values stored in the current instance.
		/// </summary>
		public override void Clear ()
		{
			throw new NotImplementedException ();
		}

		public XmpNode FindNode (string ns, string name)
		{
			if (!nodes.ContainsKey (ns))
				return null;
			if (!nodes [ns].ContainsKey (name))
				return null;
			return nodes [ns][name];

		}

		public string Render ()
		{
			XmlDocument doc = new XmlDocument (NameTable);
			var meta = CreateNode (doc, "xmpmeta", ADOBE_X_NS);
			var rdf = CreateNode (doc, "RDF", RDF_NS);
			var description = CreateNode (doc, "Description", RDF_NS);
			NodeTree.RenderInto (description);
			doc.AppendChild (meta);
			meta.AppendChild (rdf);
			rdf.AppendChild (description);
			return doc.OuterXml;
		}

		internal static XmlNode CreateNode (XmlDocument doc, string name, string ns)
		{
			if (!NamespacePrefixes.ContainsKey (ns)) {
				NamespacePrefixes.Add (ns, String.Format ("ns{0}", ++anon_ns_count));
				Console.WriteLine ("TAGLIB# DEBUG: Added {0} prefix for {1} namespace (XMP)", NamespacePrefixes [ns], ns);
			}

			return doc.CreateElement (NamespacePrefixes [ns], name, ns);
		}

		internal static XmlAttribute CreateAttribute (XmlDocument doc, string name, string ns)
		{
			if (!NamespacePrefixes.ContainsKey (ns)) {
				NamespacePrefixes.Add (ns, String.Format ("ns{0}", ++anon_ns_count));
				Console.WriteLine ("TAGLIB# DEBUG: Added {0} prefix for {1} namespace (XMP)", NamespacePrefixes [ns], ns);
			}

			return doc.CreateAttribute (NamespacePrefixes [ns], name, ns);
		}

#endregion

		private class NodeIndexVisitor : XmpNodeVisitor
		{
			private XmpTag tag;

			public NodeIndexVisitor (XmpTag tag) {
				this.tag = tag;
			}

			public void Visit (XmpNode node)
			{
				// TODO: This should be a proper check to see if it is a nodeElement
				if (node.Namespace == XmpTag.RDF_NS && node.Name == XmpTag.LI_URI)
					return;

				AddNode (node);
			}

			void AddNode (XmpNode node)
			{
				if (tag.nodes == null)
					tag.nodes = new Dictionary<string, Dictionary<string, XmpNode>> ();
				if (!tag.nodes.ContainsKey (node.Namespace))
					tag.nodes [node.Namespace] = new Dictionary<string, XmpNode> ();

				tag.nodes [node.Namespace][node.Name] = node;
			}
		}

#region Metadata fields

		/// <summary>
		///    Gets the manufacture of the recording equipment the image, the
		///    current instance belongs to, was taken with.
		/// </summary>
		/// <value>
		///    A <see cref="string" /> with the manufacture name.
		/// </value>
		public override string Make {
			get {
				var node = FindNode (XmpTag.TIFF_NS, "Make");
				return node == null ? null : node.Value;
			}
		}

		/// <summary>
		///    Gets or sets the keywords for the image described
		///    by the current instance.
		/// </summary>
		/// <value>
		///    A <see cref="string[]" /> containing the keywords of the
		///    current instace.
		/// </value>
		public override string[] Keywords {
			get {
				var node = FindNode (XmpTag.DC_NS, "subject");

				if (node == null)
					return new string [] {};

				List<string> keywords = new List<string> ();

				foreach (XmpNode child in node.Children) {
					string keyword = child.Value;
					if (keyword != null)
						keywords.Add (keyword);
				}

				return keywords.ToArray ();
			}
			set {
			}
		}

#endregion
	}
}
