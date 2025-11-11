using AutoMapper;
using OrderService.DTOs;
using OrderService.Models;

namespace OrderService.Profiles;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // Order mappings
        CreateMap<Order, OrderDto>();
        CreateMap<OrderDto, Order>();

        // OrderItem mappings
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<OrderItemDto, OrderItem>();

        // CreateOrderDto mappings
        CreateMap<CreateOrderDto, Order>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src =>
                src.OrderItems.Sum(item => item.Quantity * item.UnitPrice)))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Pending"))
            .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems));

        CreateMap<CreateOrderItemDto, OrderItem>()
            .ForMember(dest => dest.OrderItemId, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Pending"));
    }
}
