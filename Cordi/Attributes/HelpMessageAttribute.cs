using System;

namespace Cordi.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HelpMessageAttribute : Attribute
{
    public string HelpMessage { get; }

    public HelpMessageAttribute(string helpMessage)
    {
        HelpMessage = helpMessage;
    }
}
