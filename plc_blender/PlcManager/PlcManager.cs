using libplctag;
using libplctag.DataTypes;
using System.Text.Json.Nodes;


class PlcManager(ITagAttributeFactory tag_factory, string plc_address, PlcType plc_type, Protocol plc_protocol)
{
    private readonly ITagAttributeFactory _TagFactory = tag_factory ?? throw new ArgumentNullException(nameof(ITagAttributeFactory));
    private readonly string _plc_address = plc_address;
    private readonly PlcType _plc_type = plc_type;
    private readonly Protocol _plc_protocol = plc_protocol;
    private readonly Dictionary<string, ITagAttribute> attributes = [];
    public void readAllTags()
    {
        foreach (KeyValuePair<string, ITagAttribute> attr in attributes)
        {
            attr.Value.ReadTag();
        }
    }
    public void load_tags()
    {
        LoadTagsFromJson(_plc_address, _plc_type, _plc_protocol);
        identifyPlcTagsWithMapper(_plc_address, _plc_type, _plc_protocol);
    }
    private void LoadTagsFromJson(string plc_address, PlcType plc_type, Protocol plc_protocol)
    {
        string tag_def_fp = EnvVarHelper.get_tag_def_filepath()!;
        if (string.IsNullOrEmpty(tag_def_fp))
        {
            Console.WriteLine($"tag definition not set; no tags will be configured");
            return;
        }
        string jsonContent = File.ReadAllText(tag_def_fp);
        var jsonObj = JsonNode.Parse(jsonContent);
        foreach (var data in jsonObj!.AsArray())
        {
            try
            {
                string name = data!["Name"]!.ToString();
                string path = data!["Path"]!.ToString(); // WARNING :  https://github.com/libplctag/libplctag/wiki/Tag-String-Attributes
                ushort type_t = 0;
                switch (data!["DataType"]!.ToString())
                {
                    case "BOOL":
                        type_t = (ushort)TagType.BOOL;
                        break;
                    case "INT":
                        type_t = (ushort)TagType.INT;
                        break;
                    case "DINT":
                        type_t = (ushort)TagType.DINT;
                        break;
                    case "REAL":
                        type_t = (ushort)TagType.REAL;
                        break;
                    case "STRING":
                        type_t = (ushort)TagType.STRING;
                        break;
                    default:
                        Console.WriteLine($"Unknown type : {data["data_type"]}");
                        break;
                }

                TagInfo tag_info = new TagInfo
                {
                    Name = name,
                    Type = type_t
                };
                attributes.Add(name, _TagFactory.CreateTagAttribute(tag_info, plc_address, plc_type, plc_protocol));
                attributes[name].InitializeTag();
                attributes[name].SetMonitored(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up tag '{data!.ToJsonString()}' : {ex.Message}");
            }
        }
    }
    private void identifyPlcTagsWithMapper(string plc_address, PlcType plc_type, Protocol plc_protocol)
    { // NOTE : https://github.com/libplctag/libplctag.NET/issues/406
        try
        {
            var tag_infos = new Tag<TagInfoPlcMapper, TagInfo[]>()  // OBSOLETE
            {
                Gateway = plc_address,
                Path = "1,0",  // TODO ; consider looping through potential values
                PlcType = plc_type,
                Protocol = plc_protocol,
                Name = "@tags"
            };

            if (!EnvVarHelper.GetPlcStub())
            {
                tag_infos.Read();
                // _outputs.Enqueue($"{tag_infos.Value}");
                foreach (var tag_info in tag_infos.Value)
                {
                    attributes.Add(tag_info.Name, _TagFactory.CreateTagAttribute(tag_info, plc_address, plc_type, plc_protocol));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in identifyPlcTagsWithMapper: {ex.Message}");
        }
    }
    public string genTagTelemetry()
    {
        JsonNode data = new JsonObject
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["app_version"] = AppInfo._appVersion,
            ["plc_address"] = _plc_address,
            ["plc_type"] = _plc_type.ToString(),
            ["plc_protocol"] = _plc_protocol.ToString(),
            ["tags"] = new JsonObject()
        };

        foreach (KeyValuePair<string, ITagAttribute> attr in attributes)
        {
            TagType type = attr.Value.GetTagType();
            string name = attr.Value.GetTagName();
            try
            {
                if (attr.Value.IsChanged())
                {
                    switch (type)
                    {
                        case TagType.REAL:
                            data["tags"]![name] = attr.Value.GetDoubleTagValue(0);
                            break;
                        case TagType.BOOL:
                            data["tags"]![name] = attr.Value.GetBoolTagValue(0);
                            break;
                        case TagType.SINT:
                            data["tags"]![name] = attr.Value.GetSintTagValue(0);
                            break;
                        case TagType.INT:
                            data["tags"]![name] = attr.Value.GetIntTagValue(0);
                            break;
                        case TagType.DINT:
                            data["tags"]![name] = attr.Value.GetDintTagValue(0);
                            break;
                        case TagType.STRING:
                            data["tags"]![name] = attr.Value.GetStringTagValue(0);
                            break;
                        default:
                            Console.WriteLine($"Unknown type : {type}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReadTags for tag '{name}', type '{type}' : {ex.Message}");
            }
        }
        return data.ToJsonString();
    }
}