using Harp.Generators;
using YamlDotNet.RepresentationModel;

namespace Harp.Toolkit.Generate;

sealed class DocfxRegisterReference
{
    const string ReferenceHeader = "### YamlMime:UniversalReference";
    const string TableOfContentsHeader = "### YamlMime:TableOfContent";
    const string RegisterIndexFileName = "registers.md";
    static readonly string[] IndexHeaders = ["Name", "Address", "Type", "Access", "Default&nbsp;[range]", "Description"];

    readonly Dictionary<string, YamlMappingNode> pageNodes;
    readonly string targetNamespace;

    public DocfxRegisterReference(IEnumerable<KeyValuePair<string, string>> generatedPages, string ns)
    {
        var pages = new Dictionary<string, string>(generatedPages);
        pageNodes = pages
            .Where(page => page.Key != DocumentationImplementation.TocFileName)
            .ToDictionary(page => page.Key, page => Load(page.Value));
        targetNamespace = ns;

        foreach (var (key, pageNode) in pageNodes)
        {
            var item = GetRootItem(pageNode);
            if (GetKind(item) != "register")
                continue;

            AddPayload(item);
            AddAliases(pageNode, item);
            pages[key] = Save(pageNode, ReferenceHeader);
        }

        var registers = GetRegisterEntries(pages[DocumentationImplementation.TocFileName]);
        pages[DocumentationImplementation.TocFileName] = Save(registers, TableOfContentsHeader);
        pages[RegisterIndexFileName] = GetRegisterIndex(registers);
        Pages = pages;
    }

    public IReadOnlyDictionary<string, string> Pages { get; }

    void AddPayload(YamlMappingNode item)
    {
        var registerDetails = (YamlMappingNode)GetHarpDetails(item)["register"];
        var target = GetTypeUid(registerDetails);
        var targetKind = GetTypeKind(target);
        var masks = new List<string>();
        if (targetKind == "payload")
        {
            var members = new YamlSequenceNode();
            foreach (YamlMappingNode source in GetTypeItems(target).Skip(1))
            {
                var memberDetails = (YamlMappingNode)GetHarpDetails(source)["member"];
                var member = new YamlMappingNode { { "uid", source["uid"] }, { "name", source["name"] } };
                if (source.Children.ContainsKey("summary"))
                    member.Add("summary", source["summary"]);
                foreach (var (key, value) in memberDetails)
                    member.Add(key, value);
                members.Add(member);

                var memberType = GetTypeUid(memberDetails);
                if (GetTypeKind(memberType) is "bitMask" or "groupMask" && !masks.Contains(memberType))
                    masks.Add(memberType);
            }
            registerDetails.Add("members", members);
        }
        else if (targetKind is "bitMask" or "groupMask")
        {
            masks.Add(target);
        }

        if (masks.Count > 0)
            registerDetails.Add("masks", new YamlSequenceNode(masks.Select(GetMaskEntry)));
    }

    void AddAliases(YamlMappingNode pageNode, YamlMappingNode item)
    {
        var uid = GetScalar(item, "uid");
        var name = GetScalar(item, "name");
        var aliases = new YamlSequenceNode();
        foreach (var alias in new[] { $"Timestamped{name}", $"Create{name}Payload", $"CreateTimestamped{name}Payload" })
        {
            var aliasUid = $"{targetNamespace}.{alias}";
            aliases.Add(aliasUid);
            GetItems(pageNode).Add(new YamlMappingNode
            {
                { "uid", aliasUid },
                { "id", alias },
                { "name", alias },
                { "fullName", aliasUid },
                { "parent", uid },
                { "type", "class" },
                { "langs", new YamlSequenceNode("csharp") },
            });
        }
        item.Add("children", aliases);
    }

    YamlNode GetMaskEntry(string uid)
    {
        var items = GetTypeItems(uid);
        var mask = (YamlMappingNode)items[0];
        var kind = GetKind(mask);
        var entry = new YamlMappingNode { { "uid", mask["uid"] }, { "name", mask["name"] }, { "kind", kind } };
        entry.Add("type", ((YamlMappingNode)GetHarpDetails(mask)[kind])["type"]);
        if (mask.Children.ContainsKey("summary"))
            entry.Add("summary", mask["summary"]);

        var values = new YamlSequenceNode();
        foreach (YamlMappingNode source in items.Skip(1))
        {
            var value = new YamlMappingNode { { "uid", source["uid"] }, { "name", source["name"] } };
            value.Add("value", ((YamlMappingNode)GetHarpDetails(source)["maskValue"])["value"]);
            if (source.Children.ContainsKey("summary"))
                value.Add("summary", source["summary"]);
            values.Add(value);
        }
        entry.Add("values", values);
        return entry;
    }

    static YamlSequenceNode GetRegisterEntries(string tableOfContents)
    {
        var sections = (YamlSequenceNode)LoadNode(tableOfContents);
        var registers = sections.Cast<YamlMappingNode>().FirstOrDefault(section => GetScalar(section, "name") == "Registers");
        return registers is not null ? (YamlSequenceNode)registers["items"] : new YamlSequenceNode();
    }

    string GetRegisterIndex(YamlSequenceNode registers)
    {
        var rows = registers.Cast<YamlMappingNode>().Select(entry =>
        {
            var item = GetRootItem(GetPageNode(GetScalar(entry, "uid")));
            var registerDetails = (YamlMappingNode)GetHarpDetails(item)["register"];
            var length = GetScalar(registerDetails, "length");
            var access = registerDetails.Children.TryGetValue("access", out var node) && node is YamlSequenceNode sequence
                ? string.Join(", ", sequence.Cast<YamlScalarNode>().Select(scalar => scalar.Value))
                : "";
            var minValue = GetScalar(registerDetails, "minValue");
            var maxValue = GetScalar(registerDetails, "maxValue");
            var range = minValue != "" || maxValue != "" ? $"[{minValue}:{maxValue}]" : "";
            return new[]
            {
                $"[`{GetScalar(item, "name")}`](xref:{GetScalar(item, "uid")})",
                GetScalar(registerDetails, "address"),
                GetScalar(registerDetails, "payloadType") + (length != "" ? $"[{length}]" : ""),
                access,
                string.Join(" ", new[] { GetScalar(registerDetails, "defaultValue"), range }.Where(part => part != "")),
                GetScalar(item, "summary").Replace("|", "\\|"),
            };
        }).ToList();
        if (rows.Count == 0)
            return "";

        var columns = Enumerable.Range(0, IndexHeaders.Length).Where(column => rows.Any(row => row[column] != "")).ToList();
        var lines = new List<string>
        {
            "| " + string.Join(" | ", columns.Select(column => IndexHeaders[column])) + " |",
            "|" + string.Concat(columns.Select(_ => "-|")),
        };
        lines.AddRange(rows.Select(row => "| " + string.Join(" | ", columns.Select(column => row[column])) + " |"));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    YamlMappingNode GetPageNode(string uid) => pageNodes[DocumentationImplementation.GetPageFileName(uid)];

    YamlSequenceNode GetTypeItems(string uid) => GetItems(GetPageNode(uid));

    string GetTypeKind(string uid) =>
        pageNodes.TryGetValue(DocumentationImplementation.GetPageFileName(uid), out var pageNode) ? GetKind(GetRootItem(pageNode)) : "";

    static YamlNode LoadNode(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents[0].RootNode;
    }

    static YamlMappingNode Load(string text) => (YamlMappingNode)LoadNode(text);

    static string Save(YamlNode node, string header)
    {
        var writer = new StringWriter();
        writer.WriteLine(header);
        new YamlStream(new YamlDocument(node)).Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    static YamlSequenceNode GetItems(YamlMappingNode pageNode) => (YamlSequenceNode)pageNode["items"];

    static YamlMappingNode GetRootItem(YamlMappingNode pageNode) => (YamlMappingNode)GetItems(pageNode)[0];

    static YamlMappingNode GetHarpDetails(YamlMappingNode item) => (YamlMappingNode)item["harp"];

    static string GetKind(YamlMappingNode item) => ((YamlScalarNode)GetHarpDetails(item).Children.Keys.Single()).Value ?? "";

    static string GetTypeUid(YamlMappingNode node) => node.Children.TryGetValue("interfaceType", out var type) ? GetScalar(type, "uid") : "";

    static string GetScalar(YamlNode node, string key)
    {
        return node is YamlMappingNode mapping && mapping.Children.TryGetValue(key, out var value) && value is YamlScalarNode scalar
            ? scalar.Value ?? ""
            : "";
    }
}
