using AutoMapper;
using UserService.Application.DTOs;
using UserService.Domain.Entities;

namespace UserService.Application.Mappings;

/// <summary>
/// AutoMapper profile for Seller entities
/// </summary>
public class SellerMappingProfile : Profile
{
    public SellerMappingProfile()
    {
        // SellerProfile mappings
        CreateMap<SellerProfile, SellerProfileDto>()
            .ForMember(dest => dest.Name, opt => opt.Ignore())
            .ForMember(dest => dest.Email, opt => opt.Ignore());

        // SellerBookListing mappings
        CreateMap<SellerBookListing, SellerBookListingDto>();
    }
}


