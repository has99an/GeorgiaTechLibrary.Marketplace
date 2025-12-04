using AutoMapper;
using UserService.Application.DTOs;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;

namespace UserService.Application.Mappings;

/// <summary>
/// AutoMapper profile for User entity mappings
/// </summary>
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        // Address value object to DTO
        CreateMap<Address, AddressDto>()
            .ReverseMap();

        // Domain Entity to DTO
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.GetEmailString()))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()))
            .ForMember(dest => dest.DeliveryAddress, opt => opt.MapFrom(src => src.DeliveryAddress));

        // DTO to Domain Entity (for creation)
        CreateMap<CreateUserDto, User>()
            .ConvertUsing((src, dest, context) =>
            {
                var role = Domain.ValueObjects.UserRoleExtensions.ParseRole(src.Role);
                var user = User.Create(src.Email, src.Name, role);
                
                if (src.DeliveryAddress != null)
                {
                    var address = Address.Create(
                        src.DeliveryAddress.Street,
                        src.DeliveryAddress.City,
                        src.DeliveryAddress.PostalCode,
                        src.DeliveryAddress.State,
                        src.DeliveryAddress.Country);
                    user.UpdateProfile(address: address);
                }
                
                return user;
            });
    }
}

