using AutoMapper;
using WarehouseService.DTOs;
using WarehouseService.Models;

namespace WarehouseService.Profiles;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // WarehouseItem -> WarehouseItemDto
        CreateMap<WarehouseItem, WarehouseItemDto>();

        // CreateWarehouseItemDto -> WarehouseItem
        CreateMap<CreateWarehouseItemDto, WarehouseItem>();

        // UpdateWarehouseItemDto -> WarehouseItem (for updates)
        CreateMap<UpdateWarehouseItemDto, WarehouseItem>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
