using System.Collections.Generic;

namespace Valyria.Core.DTOs;

public class DataCollection<T>
{
    public int TotalCount { get; set; }
    public IEnumerable<T> Results { get; set; }
}