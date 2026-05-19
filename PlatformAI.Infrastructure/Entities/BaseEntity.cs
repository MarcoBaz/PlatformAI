using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlatformAI.Infrastructure;


public class BaseEntity
{
    [Key]
    public Guid Id { get; set; }
}

public class Entity : BaseEntity
{
    public string UserCreate { get; set; }
    public DateTime CreateDate { get; set; }
    public string UserModify { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public DateTime? ValidityDate { get; set; }
    public string? LogMessage { get; set; }
}

public class HistoricalEntity : Entity
{
    public Guid RowNumber { get; set; }
}
