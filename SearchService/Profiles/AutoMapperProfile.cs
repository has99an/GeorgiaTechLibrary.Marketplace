using AutoMapper;
using SearchService.DTOs;
using SearchService.Models;
using SearchService.Repositories;

namespace SearchService.Profiles;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // Existing mapping
        CreateMap<BookSearchModel, SearchResultDto>();
        
        // New mappings for repository models to DTOs
        CreateMap<SellerInfo, SellerInfoDto>();
        CreateMap<SearchStats, SearchStatsDto>();
        
        // Additional mappings for warehouse data
        CreateMap<WarehouseItem, SellerInfoDto>()
            .ForMember(dest => dest.SellerId, opt => opt.MapFrom(src => src.SellerId))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price))
            .ForMember(dest => dest.Condition, opt => opt.MapFrom(src => src.Condition))
            .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity));
    }
}