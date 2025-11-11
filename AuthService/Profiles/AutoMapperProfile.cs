using AutoMapper;
using AuthService.DTOs;
using AuthService.Models;

namespace AuthService.Profiles;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // AuthUser -> AuthUserEvent
        CreateMap<AuthUser, AuthUserEvent>();
    }
}
