using AutoMapper;
using StokCari.Core.Dtos;
using StokCari.Core.Entities;

namespace StokCari.Services.Mapping;

/// <summary>Entity -> DTO eşlemeleri. Aynı adlı alanlar otomatik eşlenir; yalnızca farklı olanlar burada tanımlanır.</summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Company, CompanyDto>();
        CreateMap<User, UserDto>();
        CreateMap<Stock, StockDto>();
        CreateMap<Account, AccountDto>();

        // Stok ve cari adları entity'de yok; ReceiptService toplu sorguyla doldurur.
        CreateMap<Receipt, ReceiptDto>()
            .ForMember(d => d.ReceiptNo, o => o.MapFrom(s => s.Series + "-" + s.Number))
            .ForMember(d => d.AccountName, o => o.Ignore())
            .ForMember(d => d.StockName, o => o.Ignore());
    }
}
