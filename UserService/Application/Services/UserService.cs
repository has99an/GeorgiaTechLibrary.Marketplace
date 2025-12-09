using AutoMapper;
using Microsoft.Extensions.Logging;
using UserService.Application.DTOs;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;
using UserService.Domain.Exceptions;
using UserService.Domain.ValueObjects;

namespace UserService.Application.Services;

/// <summary>
/// Service implementation for user business logic
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ISellerRepository _sellerRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly IMapper _mapper;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        ISellerRepository sellerRepository,
        IMessageProducer messageProducer,
        IMapper mapper,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _sellerRepository = sellerRepository;
        _messageProducer = messageProducer;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user == null ? null : _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        return user == null ? null : _mapper.Map<UserDto>(user);
    }

    public async Task<PagedResultDto<UserDto>> GetAllUsersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (users, totalCount) = await _userRepository.GetAllAsync(page, pageSize, cancellationToken);
        
        return new PagedResultDto<UserDto>
        {
            Items = _mapper.Map<IEnumerable<UserDto>>(users),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResultDto<UserDto>> SearchUsersAsync(UserSearchDto searchDto, CancellationToken cancellationToken = default)
    {
        UserRole? role = null;
        if (!string.IsNullOrWhiteSpace(searchDto.Role))
        {
            role = UserRoleExtensions.ParseRole(searchDto.Role);
        }

        var (users, totalCount) = await _userRepository.SearchAsync(
            searchDto.SearchTerm ?? string.Empty,
            role,
            searchDto.Page,
            searchDto.PageSize,
            cancellationToken);

        return new PagedResultDto<UserDto>
        {
            Items = _mapper.Map<IEnumerable<UserDto>>(users),
            TotalCount = totalCount,
            Page = searchDto.Page,
            PageSize = searchDto.PageSize
        };
    }

    public async Task<IEnumerable<UserDto>> GetUsersByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetByRoleAsync(role, cancellationToken);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto createDto, CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        var emailExists = await _userRepository.EmailExistsAsync(createDto.Email, cancellationToken);
        if (emailExists)
        {
            throw new DuplicateEmailException(createDto.Email);
        }

        // Create domain entity
        var role = UserRoleExtensions.ParseRole(createDto.Role);
        var user = User.Create(createDto.Email, createDto.Name, role);

        // Persist
        var createdUser = await _userRepository.AddAsync(user, cancellationToken);

        _logger.LogInformation("User created: {UserId}, Email: {Email}", 
            createdUser.UserId, createdUser.GetMaskedEmail());

        // Publish event
        PublishUserEvent(createdUser, "UserCreated");

        return _mapper.Map<UserDto>(createdUser);
    }

    public async Task<UserDto> CreateUserWithIdAsync(Guid userId, CreateUserDto createDto, CancellationToken cancellationToken = default)
    {
        // Check if user already exists with this ID
        var existingUser = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (existingUser != null)
        {
            return _mapper.Map<UserDto>(existingUser);
        }

        // Check if email already exists (but allow if it's the same user)
        var emailExists = await _userRepository.EmailExistsAsync(createDto.Email, cancellationToken);
        if (emailExists)
        {
            // Check if the existing user with this email has the same UserId
            var existingUserByEmail = await _userRepository.GetByEmailAsync(createDto.Email, cancellationToken);
            if (existingUserByEmail != null && existingUserByEmail.UserId != userId)
            {
                throw new DuplicateEmailException(createDto.Email);
            }
            // If same user, just return existing user
            if (existingUserByEmail != null && existingUserByEmail.UserId == userId)
            {
                return _mapper.Map<UserDto>(existingUserByEmail);
            }
        }

        // Create domain entity with specific UserId
        var role = UserRoleExtensions.ParseRole(createDto.Role);
        var user = User.CreateWithId(userId, createDto.Email, createDto.Name, role, DateTime.UtcNow);

        // Handle delivery address if provided
        if (createDto.DeliveryAddress != null)
        {
            var address = Address.Create(
                createDto.DeliveryAddress.Street,
                createDto.DeliveryAddress.City,
                createDto.DeliveryAddress.PostalCode,
                createDto.DeliveryAddress.State,
                createDto.DeliveryAddress.Country);
            user.UpdateProfile(address: address);
        }

        // Persist
        var createdUser = await _userRepository.AddAsync(user, cancellationToken);

        // Don't publish UserCreated event here - it's already been published by AuthService
        // Publishing again would cause duplicate events

        return _mapper.Map<UserDto>(createdUser);
    }

    public async Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserDto updateDto, UserRole? requesterRole = null, Guid? requesterId = null, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        // Check email uniqueness if changing
        if (!string.IsNullOrWhiteSpace(updateDto.Email) && updateDto.Email != user.GetEmailString())
        {
            var emailExists = await _userRepository.EmailExistsAsync(updateDto.Email, cancellationToken);
            if (emailExists)
            {
                throw new DuplicateEmailException(updateDto.Email);
            }
        }

        // Convert address DTO to value object if provided
        Address? address = null;
        if (updateDto.DeliveryAddress != null)
        {
            address = Address.Create(
                updateDto.DeliveryAddress.Street,
                updateDto.DeliveryAddress.City,
                updateDto.DeliveryAddress.PostalCode,
                updateDto.DeliveryAddress.State,
                updateDto.DeliveryAddress.Country);
        }

        // Update profile
        user.UpdateProfile(updateDto.Name, updateDto.Email, address);

        // Update role if specified - only admins can change roles
        if (!string.IsNullOrWhiteSpace(updateDto.Role))
        {
            var newRole = UserRoleExtensions.ParseRole(updateDto.Role);
            
            // Check if requester has permission to change role
            if (requesterRole != UserRole.Admin)
            {
                throw new UnauthorizedException("Only admins can change user roles");
            }
            
            // Prevent users from promoting themselves to Admin
            if (requesterId == userId && newRole == UserRole.Admin && requesterRole != UserRole.Admin)
            {
                throw new UnauthorizedException("Users cannot promote themselves to Admin");
            }
            
            user.ChangeRole(newRole);
        }

        // Persist
        var updatedUser = await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User updated: {UserId}, Email: {Email}", 
            updatedUser.UserId, updatedUser.GetMaskedEmail());

        // Publish event
        PublishUserEvent(updatedUser, "UserUpdated");

        return _mapper.Map<UserDto>(updatedUser);
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        user.Delete();
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User deleted: {UserId}", userId);

        // Publish event
        PublishUserEvent(user, "UserDeleted");

        return true;
    }

    public async Task<UserDto> ChangeUserRoleAsync(Guid userId, UserRole newRole, CancellationToken cancellationToken = default)
    {
        return await ChangeUserRoleAsync(userId, newRole, null, cancellationToken);
    }

    public async Task<UserDto> ChangeUserRoleAsync(Guid userId, UserRole newRole, string? sellerLocation, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        var oldRole = user.Role;
        user.ChangeRole(newRole);
        
        var updatedUser = await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User role changed: {UserId}, From: {OldRole}, To: {NewRole}", 
            userId, oldRole, newRole);

        // If role changed to Seller, create seller profile if it doesn't exist
        if (newRole == UserRole.Seller && oldRole != UserRole.Seller)
        {
            try
            {
                var existingProfile = await _sellerRepository.GetByUserIdAsync(userId, cancellationToken);
                if (existingProfile == null)
                {
                    var sellerProfile = Domain.Entities.SellerProfile.Create(userId, sellerLocation);
                    await _sellerRepository.AddAsync(sellerProfile, cancellationToken);
                    
                    _logger.LogInformation("Seller profile created automatically for user: {UserId}, Location: {Location}", 
                        userId, sellerLocation ?? "null");
                    
                    // Publish SellerCreated event
                    var sellerEvent = new SellerCreatedEventDto
                    {
                        SellerId = sellerProfile.SellerId,
                        UserId = sellerProfile.SellerId,
                        Email = updatedUser.GetEmailString(),
                        Name = updatedUser.Name,
                        Location = sellerProfile.Location,
                        CreatedDate = sellerProfile.CreatedDate
                    };
                    _messageProducer.SendMessage(sellerEvent, "SellerCreated");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create seller profile for user: {UserId}", userId);
                // Don't throw - role change should succeed even if seller profile creation fails
            }
        }

        // Publish event
        PublishUserEvent(updatedUser, "UserRoleChanged");

        return _mapper.Map<UserDto>(updatedUser);
    }

    public async Task<UserDto> UpgradeToSellerAsync(Guid userId, string? location, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        // If user is already a Seller, ensure seller profile exists and update location if provided
        if (user.Role == UserRole.Seller)
        {
            var existingProfile = await _sellerRepository.GetByUserIdAsync(userId, cancellationToken);
            
            // If profile doesn't exist, create it (handles data inconsistency)
            if (existingProfile == null)
            {
                var sellerProfile = Domain.Entities.SellerProfile.Create(userId, location);
                await _sellerRepository.AddAsync(sellerProfile, cancellationToken);
                
                _logger.LogInformation("Seller profile created for existing seller user: {UserId}, Location: {Location}", 
                    userId, location ?? "null");
                
                // Publish SellerCreated event
                var sellerEvent = new SellerCreatedEventDto
                {
                    SellerId = sellerProfile.SellerId,
                    UserId = sellerProfile.SellerId,
                    Email = user.GetEmailString(),
                    Name = user.Name,
                    Location = sellerProfile.Location,
                    CreatedDate = sellerProfile.CreatedDate
                };
                _messageProducer.SendMessage(sellerEvent, "SellerCreated");
            }
            else if (!string.IsNullOrWhiteSpace(location))
            {
                // Update location if provided
                existingProfile.UpdateLocation(location);
                await _sellerRepository.UpdateAsync(existingProfile, cancellationToken);
                _logger.LogInformation("Updated location for existing seller: {UserId}, Location: {Location}", 
                    userId, location);
            }
            
            return _mapper.Map<UserDto>(user);
        }

        // Change role to Seller and create seller profile with location
        return await ChangeUserRoleAsync(userId, UserRole.Seller, location, cancellationToken);
    }

    public async Task<object> ExportUserDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        _logger.LogInformation("User data exported: {UserId}", userId);

        // Return all user data for GDPR compliance
        return new
        {
            user.UserId,
            Email = user.GetEmailString(),
            user.Name,
            Role = user.Role.ToString(),
            user.CreatedDate,
            user.UpdatedDate,
            ExportDate = DateTime.UtcNow
        };
    }

    public async Task<bool> AnonymizeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        user.Anonymize();
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User anonymized: {UserId}", userId);

        return true;
    }

    public async Task<Dictionary<string, int>> GetRoleStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _userRepository.GetRoleStatisticsAsync(cancellationToken);
        return stats.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value
        );
    }

    private void PublishUserEvent(User user, string eventType)
    {
        try
        {
            var userEvent = new UserEventDto
            {
                UserId = user.UserId,
                Email = user.GetEmailString(),
                Name = user.Name,
                Role = user.Role.ToString(),
                CreatedDate = user.CreatedDate,
                EventType = eventType
            };

            _messageProducer.SendMessage(userEvent, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish user event: {EventType}, UserId: {UserId}", 
                eventType, user.UserId);
            // Don't throw - event publishing failure shouldn't fail the operation
        }
    }
}

