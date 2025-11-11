using AutoMapper;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Profiles;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // User -> UserDto
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));

        // User -> UserEvent
        CreateMap<User, UserEvent>();

        // CreateUserDto -> User
        CreateMap<CreateUserDto, User>();

        // UpdateUserDto -> User (for updates)
        CreateMap<UpdateUserDto, User>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
