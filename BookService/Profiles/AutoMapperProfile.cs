using AutoMapper;
using BookService.DTOs;
using BookService.Models;

namespace BookService.Profiles;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // Book -> BookDto
        CreateMap<Book, BookDto>();

        // Book -> BookEvent
        CreateMap<Book, BookEvent>();

        // CreateBookDto -> Book
        CreateMap<CreateBookDto, Book>();

        // UpdateBookDto -> Book (for updates)
        CreateMap<UpdateBookDto, Book>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}