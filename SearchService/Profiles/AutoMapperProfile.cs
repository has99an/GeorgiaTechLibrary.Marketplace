using AutoMapper;
using SearchService.DTOs;
using SearchService.Models;
using SearchService.Repositories;

namespace SearchService.Profiles;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // Existing mapping - OPDATERET med nye felter
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

        // 👇 NY MAPPING for BookEvent til BookSearchModel
        CreateMap<BookEvent, BookSearchModel>()
            .ForMember(dest => dest.Isbn, opt => opt.MapFrom(src => src.ISBN))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.BookTitle))
            .ForMember(dest => dest.Author, opt => opt.MapFrom(src => src.BookAuthor))
            .ForMember(dest => dest.YearOfPublication, opt => opt.MapFrom(src => src.YearOfPublication))
            .ForMember(dest => dest.Publisher, opt => opt.MapFrom(src => src.Publisher))
            .ForMember(dest => dest.ImageUrlS, opt => opt.MapFrom(src => src.ImageUrlS))
            .ForMember(dest => dest.ImageUrlM, opt => opt.MapFrom(src => src.ImageUrlM))
            .ForMember(dest => dest.ImageUrlL, opt => opt.MapFrom(src => src.ImageUrlL))
            // 👇 MAP DE 8 NYE FELTER
            .ForMember(dest => dest.Genre, opt => opt.MapFrom(src => src.Genre))
            .ForMember(dest => dest.Language, opt => opt.MapFrom(src => src.Language))
            .ForMember(dest => dest.PageCount, opt => opt.MapFrom(src => src.PageCount))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => src.Rating))
            .ForMember(dest => dest.AvailabilityStatus, opt => opt.MapFrom(src => src.AvailabilityStatus))
            .ForMember(dest => dest.Edition, opt => opt.MapFrom(src => src.Edition))
            .ForMember(dest => dest.Format, opt => opt.MapFrom(src => src.Format));
    }
}