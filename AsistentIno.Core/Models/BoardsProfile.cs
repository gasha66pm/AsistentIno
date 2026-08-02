using System;
using System.Collections.Generic;
using System.Text;

namespace AsistentIno.Models;

public sealed class BoardProfile
{
    public string Name { get; set; } = string.Empty;
    public string Fqbn { get; set; } = string.Empty;
    public override string ToString() => $"{Name} ({Fqbn})";
}