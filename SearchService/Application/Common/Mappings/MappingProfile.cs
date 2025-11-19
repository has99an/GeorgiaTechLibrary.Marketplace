using AutoMapper;
using SearchService.Application.Common.Models;
using SearchService.Domain.Entities;

namespace SearchService.Application.Common.Mappings;

/// <summary>
/// AutoMapper profile for Application Layer mappings
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Book to BookDto
        CreateMap<Book, BookDto>()
            .ForMember(dest => dest.Isbn, opt => opt.MapFrom(src => src.Isbn.Value))
            .ForMember(dest => dest.TotalStock, opt => opt.MapFrom(src => src.Stock.TotalStock))
            .ForMember(dest => dest.AvailableSellers, opt => opt.MapFrom(src => src.Stock.AvailableSellers))
            .ForMember(dest => dest.MinPrice, opt => opt.MapFrom(src => src.Pricing.MinPrice));
    }
}

