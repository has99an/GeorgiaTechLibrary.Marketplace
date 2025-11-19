using AutoMapper;
using UserService.Application.DTOs;
using UserService.Domain.Entities;

namespace UserService.Application.Mappings;

/// <summary>
/// AutoMapper profile for User entity mappings
/// </summary>
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        // Domain Entity to DTO
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.GetEmailString()))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));

        // DTO to Domain Entity (for creation)
        CreateMap<CreateUserDto, User>()
            .ConvertUsing((src, dest, context) =>
            {
                var role = Domain.ValueObjects.UserRoleExtensions.ParseRole(src.Role);
                return User.Create(src.Email, src.Name, role);
            });
    }
}

