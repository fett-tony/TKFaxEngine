/*
 * Managed C# port of TKFX_xml.c and TKFX_xml.h.
 *
 * This is the same compact XML tree parser and bundled schema validator used
 * by the supplied TKFaxEngine C tests. It intentionally supports the XML
 * constructs required by fax-tests.xml and global-tones.xml and ignores text
 * node content exactly like the source implementation.
 */

#nullable enable

using System.Globalization;
using System.Text;

namespace TKFaxEngineTest.System;

public sealed class xmlAttr {
    public string name = string.Empty;
    public string value = string.Empty;
    public xmlAttr? next;
}

public sealed class xmlNode {
    public string name = string.Empty;
    public xmlNode? xmlChildrenNode;
    public xmlNode? last;
    public xmlNode? next;
    public xmlNode? parent;
    public xmlAttr? properties;
    public int type;
}

public sealed class xmlDoc {
    public xmlNode? root;
}

public sealed class xmlParserCtxt {
    public string error = string.Empty;
    public int valid;
}


public static class TKFXXml {
    public const int TKFaxEngine_XML_PTR_FALLBACK = 1;
    public const int XML_PARSE_XINCLUDE = 0x0000_0400;
    public const int XML_PARSE_DTDVALID = 0x0000_0010;

    private const int TKFX_XML_ELEMENT_NODE = 1;
    private const int TKFX_XML_TEXT_NODE = 3;

    private static int keepBlanks = 1;

    private sealed class XmlParser {
        internal readonly string Text;
        internal readonly int End;
        internal readonly xmlParserCtxt? Context;
        internal int Cursor;

        internal XmlParser(string text, xmlParserCtxt? context) {
            Text = text;
            End = text.Length;
            Context = context;
        }
    }

    private static void set_error(xmlParserCtxt? context, string? message) {
        if (context is null)
            return;

        context.error = message ?? "XML parse error";
    }

    private static string dup_range(string source, int begin, int end) {
        if (begin < 0 || end < begin || end > source.Length)
            throw new ArgumentOutOfRangeException(nameof(begin));

        return source.Substring(begin, end - begin);
    }

    private static void skip_ws(XmlParser parser) {
        while (parser.Cursor < parser.End && IsSpace(parser.Text[parser.Cursor]))
            parser.Cursor++;
    }

    private static bool IsSpace(char value) {
        return value is ' ' or '\t' or '\r' or '\n' or '\f' or '\v';
    }

    private static bool IsAsciiAlpha(char value) {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static bool IsAsciiDigit(char value) {
        return value is >= '0' and <= '9';
    }

    private static bool starts_with(XmlParser parser, string text) {
        int remaining = parser.End - parser.Cursor;
        return remaining >= text.Length &&
               parser.Text.AsSpan(parser.Cursor, text.Length).SequenceEqual(text.AsSpan());
    }

    private static int skip_until(XmlParser parser, string terminator) {
        int hit = parser.Text.IndexOf(terminator, parser.Cursor, StringComparison.Ordinal);
        if (hit < 0 || hit >= parser.End)
            return -1;

        parser.Cursor = hit + terminator.Length;
        return 0;
    }

    private static string decode_entities(string source, int begin, int end) {
        StringBuilder output = new(end - begin);
        int cursor = begin;

        while (cursor < end) {
            if (source[cursor] == '&') {
                int semi = source.IndexOf(';', cursor, end - cursor);
                if (semi >= 0) {
                    string? replacement = null;
                    int entityLength = semi - cursor + 1;

                    if (entityLength == 4 &&
                        source.AsSpan(cursor, entityLength).SequenceEqual("&lt;".AsSpan())) {
                        replacement = "<";
                    } else if (entityLength == 4 &&
                               source.AsSpan(cursor, entityLength).SequenceEqual("&gt;".AsSpan())) {
                        replacement = ">";
                    } else if (entityLength == 5 &&
                               source.AsSpan(cursor, entityLength).SequenceEqual("&amp;".AsSpan())) {
                        replacement = "&";
                    } else if (entityLength == 6 &&
                               source.AsSpan(cursor, entityLength).SequenceEqual("&quot;".AsSpan())) {
                        replacement = "\"";
                    } else if (entityLength == 6 &&
                               source.AsSpan(cursor, entityLength).SequenceEqual("&apos;".AsSpan())) {
                        replacement = "'";
                    } else if (cursor + 2 < semi && source[cursor + 1] == '#') {
                        int numberStart = cursor + 2;
                        NumberStyles style = NumberStyles.Integer;
                        if (source[numberStart] is 'x' or 'X') {
                            style = NumberStyles.AllowHexSpecifier;
                            numberStart++;
                        }

                        ReadOnlySpan<char> number = source.AsSpan(numberStart, semi - numberStart);
                        if (number.Length > 0 &&
                            long.TryParse(number, style, CultureInfo.InvariantCulture, out long value) &&
                            value > 0 && value <= 0x7F) {
                            replacement = ((char)value).ToString();
                        }
                    }

                    if (replacement is not null) {
                        output.Append(replacement[0]);
                        cursor = semi + 1;
                        continue;
                    }
                }
            }

            output.Append(source[cursor]);
            cursor++;
        }

        return output.ToString();
    }

    private static string? parse_name(XmlParser parser) {
        int begin = parser.Cursor;
        if (parser.Cursor >= parser.End)
            return null;

        char first = parser.Text[parser.Cursor];
        if (!(IsAsciiAlpha(first) || first is '_' or ':'))
            return null;

        parser.Cursor++;
        while (parser.Cursor < parser.End) {
            char value = parser.Text[parser.Cursor];
            if (!(IsAsciiAlpha(value) || IsAsciiDigit(value) || value is '_' or '-' or '.' or ':'))
                break;
            parser.Cursor++;
        }

        return dup_range(parser.Text, begin, parser.Cursor);
    }

    private static void free_attributes(xmlAttr? attribute) {
        while (attribute is not null) {
            xmlAttr? next = attribute.next;
            attribute.name = string.Empty;
            attribute.value = string.Empty;
            attribute.next = null;
            attribute = next;
        }
    }

    private static void free_node(xmlNode? node) {
        while (node is not null) {
            xmlNode? next = node.next;
            free_node(node.xmlChildrenNode);
            free_attributes(node.properties);
            node.name = string.Empty;
            node.xmlChildrenNode = null;
            node.last = null;
            node.next = null;
            node.parent = null;
            node.properties = null;
            node = next;
        }
    }

    private static int append_child(xmlNode? parent, xmlNode? child) {
        if (parent is null || child is null)
            return -1;

        child.parent = parent;
        if (parent.xmlChildrenNode is null)
            parent.xmlChildrenNode = child;
        else
            parent.last!.next = child;
        parent.last = child;
        return 0;
    }

    private static int parse_attributes(XmlParser parser, xmlNode node) {
        xmlAttr? tail = null;

        for (; ; ) {
            skip_ws(parser);
            if (parser.Cursor >= parser.End ||
                parser.Text[parser.Cursor] == '>' ||
                starts_with(parser, "/>")) {
                return 0;
            }

            string? name = parse_name(parser);
            if (name is null) {
                set_error(parser.Context, "Invalid XML attribute name");
                return -1;
            }

            skip_ws(parser);
            if (parser.Cursor >= parser.End || parser.Text[parser.Cursor] != '=') {
                set_error(parser.Context, "Missing '=' after XML attribute name");
                return -1;
            }

            parser.Cursor++;
            skip_ws(parser);
            if (parser.Cursor >= parser.End ||
                (parser.Text[parser.Cursor] != '\'' && parser.Text[parser.Cursor] != '"')) {
                set_error(parser.Context, "XML attribute value is not quoted");
                return -1;
            }

            char quote = parser.Text[parser.Cursor++];
            int begin = parser.Cursor;
            while (parser.Cursor < parser.End && parser.Text[parser.Cursor] != quote)
                parser.Cursor++;

            if (parser.Cursor >= parser.End) {
                set_error(parser.Context, "Unterminated XML attribute value");
                return -1;
            }

            string value = decode_entities(parser.Text, begin, parser.Cursor);
            parser.Cursor++;

            xmlAttr attribute = new() {
                name = name,
                value = value
            };

            if (tail is null)
                node.properties = attribute;
            else
                tail.next = attribute;
            tail = attribute;
        }
    }

    private static int skip_misc(XmlParser parser) {
        bool progressed;
        do {
            progressed = false;
            skip_ws(parser);

            if (starts_with(parser, "<?")) {
                parser.Cursor += 2;
                if (skip_until(parser, "?>") < 0)
                    return -1;
                progressed = true;
            } else if (starts_with(parser, "<!--")) {
                parser.Cursor += 4;
                if (skip_until(parser, "-->") < 0)
                    return -1;
                progressed = true;
            } else if (starts_with(parser, "<!DOCTYPE")) {
                int bracketDepth = 0;
                char quote = '\0';
                parser.Cursor += 9;

                while (parser.Cursor < parser.End) {
                    char value = parser.Text[parser.Cursor++];
                    if (quote != '\0') {
                        if (value == quote)
                            quote = '\0';
                        continue;
                    }

                    if (value is '\'' or '"')
                        quote = value;
                    else if (value == '[')
                        bracketDepth++;
                    else if (value == ']' && bracketDepth > 0)
                        bracketDepth--;
                    else if (value == '>' && bracketDepth == 0)
                        break;
                }

                progressed = true;
            }
        } while (progressed);

        return 0;
    }

    private static xmlNode? parse_element(XmlParser parser) {
        if (skip_misc(parser) < 0 ||
            parser.Cursor >= parser.End ||
            parser.Text[parser.Cursor] != '<' ||
            starts_with(parser, "</")) {
            return null;
        }

        parser.Cursor++;
        string? name = parse_name(parser);
        if (name is null) {
            set_error(parser.Context, "Invalid XML element name");
            return null;
        }

        xmlNode node = new() {
            name = name,
            type = TKFX_XML_ELEMENT_NODE
        };

        if (parse_attributes(parser, node) < 0) {
            free_node(node);
            return null;
        }

        if (starts_with(parser, "/>")) {
            parser.Cursor += 2;
            return node;
        }

        if (parser.Cursor >= parser.End || parser.Text[parser.Cursor] != '>') {
            free_node(node);
            set_error(parser.Context, "Missing '>' after XML start tag");
            return null;
        }

        parser.Cursor++;
        for (; ; ) {
            if (parser.Cursor >= parser.End) {
                free_node(node);
                set_error(parser.Context, "Unexpected end of XML document");
                return null;
            }

            if (starts_with(parser, "</")) {
                parser.Cursor += 2;
                string? endName = parse_name(parser);
                skip_ws(parser);

                if (endName is null ||
                    parser.Cursor >= parser.End ||
                    parser.Text[parser.Cursor] != '>' ||
                    xmlStrcmp(endName, node.name) != 0) {
                    free_node(node);
                    set_error(parser.Context, "Mismatched XML end tag");
                    return null;
                }

                parser.Cursor++;
                return node;
            }

            if (starts_with(parser, "<!--")) {
                parser.Cursor += 4;
                if (skip_until(parser, "-->") < 0) {
                    free_node(node);
                    set_error(parser.Context, "Unterminated XML comment");
                    return null;
                }
                continue;
            }

            if (starts_with(parser, "<![CDATA[")) {
                parser.Cursor += 9;
                if (skip_until(parser, "]]>") < 0) {
                    free_node(node);
                    set_error(parser.Context, "Unterminated XML CDATA section");
                    return null;
                }
                continue;
            }

            if (parser.Text[parser.Cursor] == '<') {
                xmlNode? child = parse_element(parser);
                if (child is null || append_child(node, child) < 0) {
                    free_node(child);
                    free_node(node);
                    return null;
                }
            } else {
                /* The fax test documents use element/attribute data only. */
                while (parser.Cursor < parser.End && parser.Text[parser.Cursor] != '<')
                    parser.Cursor++;
            }
        }
    }

    private static xmlAttr? find_attr(xmlNode? node, string? name) {
        if (node is null || name is null)
            return null;

        for (xmlAttr? attribute = node.properties; attribute is not null; attribute = attribute.next) {
            if (string.Equals(attribute.name, name, StringComparison.Ordinal))
                return attribute;
        }

        return null;
    }

    private static bool name_in_list(string name, string[] allowed) {
        for (int i = 0; i < allowed.Length; i++) {
            if (string.Equals(name, allowed[i], StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static int validate_attributes(
        xmlParserCtxt context,
        xmlNode node,
        string[] allowed,
        string[] required) {
        for (xmlAttr? attribute = node.properties;
             attribute is not null;
             attribute = attribute.next) {
            if (!name_in_list(attribute.name, allowed)) {
                context.error =
                    $"DTD validation: attribute '{attribute.name}' is not allowed on <{node.name}>";
                return -1;
            }
        }

        for (int i = 0; i < required.Length; i++) {
            if (find_attr(node, required[i]) is null) {
                context.error =
                    $"DTD validation: required attribute '{required[i]}' is missing on <{node.name}>";
                return -1;
            }
        }

        return 0;
    }

    private static int validate_no_children(xmlParserCtxt context, xmlNode node) {
        if (node.xmlChildrenNode is not null) {
            context.error = $"DTD validation: <{node.name}> must be empty";
            return -1;
        }
        return 0;
    }

    private static int bad_child(xmlParserCtxt context, xmlNode parent, xmlNode child) {
        context.error =
            $"DTD validation: <{child.name}> is not allowed inside <{parent.name}>";
        return -1;
    }

    private static int validate_fax_node(xmlParserCtxt context, xmlNode node) {
        string[] noAttributes = [];
        string[] pathAttributes = ["type", "value"];
        string[] namedAttributes = ["name"];
        string[] repeatAttributes = ["min", "max"];
        string[] stepAttributes = [
            "dir", "type", "modem", "tag", "value", "timein", "timeout",
            "crc_error", "pattern", "bad_rows", "min_bits", "frame_size",
            "compression", "block", "xxx"
        ];
        string[] checkAttributes = ["name", "desc", "cond"];

        if (node.name == "fax-tests") {
            if (validate_attributes(context, node, noAttributes, noAttributes) < 0)
                return -1;

            for (xmlNode? child = node.xmlChildrenNode; child is not null; child = child.next) {
                if (child.name is not ("config" or "messages" or "test-group"))
                    return bad_child(context, node, child);
                if (validate_fax_node(context, child) < 0)
                    return -1;
            }
            return 0;
        }

        if (node.name is "config" or "messages") {
            string expected = node.name == "config" ? "path" : "message";
            if (validate_attributes(context, node, noAttributes, noAttributes) < 0)
                return -1;

            for (xmlNode? child = node.xmlChildrenNode; child is not null; child = child.next) {
                if (child.name != expected)
                    return bad_child(context, node, child);
                if (validate_fax_node(context, child) < 0)
                    return -1;
            }
            return 0;
        }

        if (node.name is "path" or "message") {
            if (validate_attributes(context, node, pathAttributes, pathAttributes) < 0)
                return -1;
            return validate_no_children(context, node);
        }

        if (node.name == "test-group") {
            if (validate_attributes(context, node, namedAttributes, noAttributes) < 0)
                return -1;

            for (xmlNode? child = node.xmlChildrenNode; child is not null; child = child.next) {
                if (child.name != "test")
                    return bad_child(context, node, child);
                if (validate_fax_node(context, child) < 0)
                    return -1;
            }
            return 0;
        }

        if (node.name == "test") {
            if (validate_attributes(context, node, namedAttributes, noAttributes) < 0)
                return -1;

            for (xmlNode? child = node.xmlChildrenNode; child is not null; child = child.next) {
                if (child.name is not ("repeat" or "step"))
                    return bad_child(context, node, child);
                if (validate_fax_node(context, child) < 0)
                    return -1;
            }
            return 0;
        }

        if (node.name == "repeat") {
            if (validate_attributes(context, node, repeatAttributes, noAttributes) < 0)
                return -1;

            for (xmlNode? child = node.xmlChildrenNode; child is not null; child = child.next) {
                if (child.name != "step")
                    return bad_child(context, node, child);
                if (validate_fax_node(context, child) < 0)
                    return -1;
            }
            return 0;
        }

        if (node.name == "step") {
            if (validate_attributes(context, node, stepAttributes, noAttributes) < 0)
                return -1;

            for (xmlNode? child = node.xmlChildrenNode; child is not null; child = child.next) {
                if (child.name != "check")
                    return bad_child(context, node, child);
                if (validate_fax_node(context, child) < 0)
                    return -1;
            }
            return 0;
        }

        if (node.name == "check") {
            if (validate_attributes(context, node, checkAttributes, noAttributes) < 0)
                return -1;
            return validate_no_children(context, node);
        }

        context.error = $"DTD validation: unknown fax element <{node.name}>";
        return -1;
    }

    private static bool is_tone_element(string name) {
        string[] tones = [
            "dial-tone", "ringing-tone", "busy-tone", "congestion-tone",
            "number-unobtainable-tone", "special-information-tone", "call-waiting-tone",
            "waiting-tone", "pay-tone", "payphone-recognition-tone", "warning-tone",
            "test-number-tone", "end-of-three-party-service-tone", "service-activated-tone",
            "record-tone", "facilities-tone", "offering-tone", "route-tone",
            "confirmation-tone", "intrusion-tone", "intercept-tone", "reorder-tone",
            "holding-tone", "search-tone", "acceptance-tone", "executive-override-tone",
            "function-acknowledge-tone", "queue-tone", "valid-tone-radio-paging",
            "notify-tone", "preemption-tone", "comfort-tone", "connection-tone",
            "refusal-tone", "line-lockout-tone", "permanent-signal-tone",
            "identification-tone", "positive-indication-tone", "negative-indication-tone"
        ];
        return name_in_list(name, tones);
    }

    private static int validate_tone_step(xmlParserCtxt context, xmlNode node) {
        string[] stepAttributes = [
            "cycles", "freq", "level", "length", "recorded-announcement", "recognition-length"
        ];
        string[] none = [];

        if (validate_attributes(context, node, stepAttributes, none) < 0)
            return -1;

        for (xmlNode? child = node.xmlChildrenNode; child is not null; child = child.next) {
            if (child.name != "step") {
                context.error =
                    $"DTD validation: <{child.name}> is not allowed inside <step>";
                return -1;
            }
            if (validate_tone_step(context, child) < 0)
                return -1;
        }

        return 0;
    }

    private static int validate_tones(xmlParserCtxt context, xmlNode root) {
        string[] none = [];
        string[] setAttributes = ["country", "uncode"];
        string[] toneAttributes = ["domain", "type"];

        if (root.name != "global-tones")
            return -1;
        if (validate_attributes(context, root, none, none) < 0)
            return -1;

        for (xmlNode? set = root.xmlChildrenNode; set is not null; set = set.next) {
            if (set.name != "tone-set") {
                context.error =
                    $"DTD validation: <{set.name}> is not allowed inside <global-tones>";
                return -1;
            }
            if (validate_attributes(context, set, setAttributes, setAttributes) < 0)
                return -1;

            for (xmlNode? tone = set.xmlChildrenNode; tone is not null; tone = tone.next) {
                if (!is_tone_element(tone.name)) {
                    context.error = $"DTD validation: unknown tone element <{tone.name}>";
                    return -1;
                }
                if (validate_attributes(context, tone, toneAttributes, none) < 0)
                    return -1;

                for (xmlNode? step = tone.xmlChildrenNode; step is not null; step = step.next) {
                    if (step.name != "step") {
                        context.error =
                            $"DTD validation: <{step.name}> is not allowed inside <{tone.name}>";
                        return -1;
                    }
                    if (validate_tone_step(context, step) < 0)
                        return -1;
                }
            }
        }

        return 0;
    }

    private static int validate_known_schema(xmlParserCtxt context, xmlNode? root) {
        if (root is null)
            return -1;
        if (root.name == "fax-tests")
            return validate_fax_node(context, root);
        if (root.name == "global-tones")
            return validate_tones(context, root);

        context.error =
            $"DTD validation: no bundled schema validator for root <{root.name}>";
        return -1;
    }

    public static xmlParserCtxt xmlNewParserCtxt() {
        return new xmlParserCtxt();
    }

    public static void xmlFreeParserCtxt(xmlParserCtxt? context) {
        if (context is null)
            return;

        context.error = string.Empty;
        context.valid = 0;
    }

    public static xmlDoc? xmlCtxtReadFile(
        xmlParserCtxt? context,
        string? filename,
        string? encoding,
        int options) {
        _ = encoding;
        if (context is not null) {
            context.error = string.Empty;
            context.valid = 0;
        }

        if (filename is null) {
            set_error(context, "No XML filename supplied");
            return null;
        }

        byte[] bytes;
        try {
            bytes = File.ReadAllBytes(filename);
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException) {
            set_error(context, "Cannot open XML file");
            return null;
        }

        string text = Encoding.Latin1.GetString(bytes);
        XmlParser parser = new(text, context);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            parser.Cursor = 3;

        if (skip_misc(parser) < 0) {
            set_error(context, "Invalid XML prolog");
            return null;
        }

        xmlDoc document = new() {
            root = parse_element(parser)
        };

        if (document.root is not null) {
            if (skip_misc(parser) < 0) {
                free_node(document.root);
                document.root = null;
                set_error(context, "Invalid trailing XML content");
            } else {
                skip_ws(parser);
                if (parser.Cursor != parser.End) {
                    free_node(document.root);
                    document.root = null;
                    set_error(context, "Unexpected content after XML root element");
                }
            }
        }

        if (document.root is null)
            return null;

        if ((options & XML_PARSE_DTDVALID) != 0) {
            xmlParserCtxt validationContext = context ?? new xmlParserCtxt();
            if (validate_known_schema(validationContext, document.root) < 0) {
                if (context is not null)
                    context.valid = 0;
            } else if (context is not null) {
                context.valid = 1;
            }
        } else if (context is not null) {
            context.valid = 1;
        }

        return document;
    }

    public static xmlNode? xmlDocGetRootElement(xmlDoc? document) {
        return document?.root;
    }

    public static void xmlFreeDoc(xmlDoc? document) {
        if (document is null)
            return;

        free_node(document.root);
        document.root = null;
    }

    public static string? xmlGetProp(xmlNode? node, string? name) {
        if (node is null || name is null)
            return null;

        for (xmlAttr? attribute = node.properties;
             attribute is not null;
             attribute = attribute.next) {
            if (xmlStrcmp(attribute.name, name) == 0)
                return new string(attribute.value.ToCharArray());
        }

        return null;
    }

    public static int xmlStrcmp(string? left, string? right) {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;
        return string.CompareOrdinal(left, right);
    }

    public static int xmlIsBlankNode(xmlNode? node) {
        return node is not null && node.type == TKFX_XML_TEXT_NODE ? 1 : 0;
    }

    public static void xmlFree(object? pointer) {
        _ = pointer;
    }

    public static void xmlCleanupParser() {
    }

    public static int xmlKeepBlanksDefault(int keepBlanksValue) {
        return Interlocked.Exchange(ref keepBlanks, keepBlanksValue);
    }
}
