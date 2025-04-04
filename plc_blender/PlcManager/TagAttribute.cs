using libplctag;
using libplctag.DataTypes;

class TagAttribute(TagInfo tagInfo, string address, PlcType plc_type, Protocol plc_protocol) : ITagAttribute
{
    public TagInfo TagInfo = tagInfo;
    private readonly Tag Tag = new()
    {
        Name = $"{tagInfo.Name}",
        Path = "1,0", // assuming default,
        Gateway = address,
        PlcType = plc_type,
        Protocol = plc_protocol
    };
    protected DateTime lastRead_ = new(0);
    protected DateTime lastAccessed_ = new(0);
    protected byte[] buffer_cached_ = [];
    protected DateTime lastChanged_ = new(0);
    protected bool is_monitored_ = false;
    public virtual TagType GetTagType()
    {
        return (TagType)TagInfo.Type;
    }
    public virtual string GetTagName()
    {
        return TagInfo.Name;
    }
    public virtual void InitializeTag()
    {
        Tag.Initialize();
    }
    public virtual void ReadTag()
    {
        Tag.Read();
        lastRead_ = DateTime.Now;
        byte[] buffer_temp = Tag.GetBuffer();
        if (buffer_cached_ != buffer_temp)
        {
            buffer_cached_ = buffer_temp;
            lastChanged_ = DateTime.Now;
        }
    }
    public virtual double GetDoubleTagValue(int offset = 0)
    {
        lastAccessed_ = DateTime.Now;
        return Tag.GetFloat32(offset);
    }
    public virtual bool GetBoolTagValue(int offset = 0)
    {
        lastAccessed_ = DateTime.Now;
        return Tag.GetBit(offset);
    }
    public virtual int GetSintTagValue(int offset = 0)
    {
        lastAccessed_ = DateTime.Now;
        return Tag.GetInt8(offset);
    }
    public virtual int GetIntTagValue(int offset = 0)
    {
        lastAccessed_ = DateTime.Now;
        return Tag.GetInt16(offset);
    }
    public virtual int GetDintTagValue(int offset = 0)
    {
        lastAccessed_ = DateTime.Now;
        return Tag.GetInt32(offset);
    }
    public virtual string GetStringTagValue(int offset = 0)
    {
        lastAccessed_ = DateTime.Now;
        return Tag.GetString(offset);
    }
    public bool IsChanged()
    {
        return lastAccessed_ < lastChanged_;
    }
    public bool IsMonitored()
    {
        return is_monitored_;
    }
    public void SetMonitored(bool should_monitor)
    {
        is_monitored_ = should_monitor;
    }

}
