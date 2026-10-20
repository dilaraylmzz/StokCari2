namespace StokCari.Core.Entities;

public interface ICreationAudit
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
}

public interface IAuditable : ICreationAudit
{
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

public interface ITenantEntity
{
    Guid CompanyId { get; set; }
}

/// <summary>Soft delete ve denetim alanları olan kart/belge tabloları için temel sınıf.</summary>
public abstract class BaseEntity : IAuditable, ISoftDelete
{
    // Id uygulamada üretilir (sıralı GUID, indeks parçalanmasını önler).
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>Hareket (ledger) tabloları için temel sınıf: salt eklenir, güncellenmez ve silinmez.</summary>
public abstract class LedgerEntity : ICreationAudit, ITenantEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
