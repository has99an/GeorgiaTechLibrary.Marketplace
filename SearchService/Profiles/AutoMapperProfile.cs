using AutoMapper;
using SearchService.DTOs;
using SearchService.Models;

namespace SearchService.Profiles;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<BookSearchModel, SearchResultDto>();
    }
}
