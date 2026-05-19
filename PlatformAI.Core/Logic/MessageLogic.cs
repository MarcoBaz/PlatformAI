using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using PlatformAI.Infrastructure.DTO;
using PlatformAI.Infrastructure.Master;
using PlatformAI.Infrastructure.VM;

namespace PlatformAI.Core.Logic;

public class MessageLogic
{
    private IUnitOfWork _uow;
    private IServiceProvider _serviceProvider;

    private IRepository<Conversation> _conversationRepository;

    public MessageLogic(IUnitOfWork uow, IServiceProvider serviceProvider)
    {
        _uow = uow;
        _serviceProvider = serviceProvider;
        _conversationRepository = _uow.Repository<Conversation>();
    }
    public async Task<List<ConversationVM>> GetAllConversations(Guid userId)
    {
        var conversations = await _conversationRepository.Query(x => x.UserId == userId)
                         .Include(x => x.Messages)
                         .ToListAsync();
        return InfrastructureUtil.MapperManager.Map<List<Conversation>, List<ConversationVM>>(conversations);

    }

    public async Task<ConversationVM> SaveConversationAsync(MessageVM messageVM,Guid userId)
    {
         var message = InfrastructureUtil.MapperManager.Map<MessageVM, Message>(messageVM);
        try
        {
            await _uow.BeginTransactionAsync();
            var existingConversation = await _conversationRepository.Query(x => x.Id == Guid.Parse(messageVM.ConversationId)).FirstOrDefaultAsync();
            if (existingConversation == null)
            {
                existingConversation = new Conversation();
                existingConversation.Title = messageVM.Content.Length > 20 ? messageVM.Content.Substring(0, 20) : messageVM.Content;
                existingConversation.UserId = userId;
                await _conversationRepository.AddAsync(existingConversation);
            }
            message.ConversationId = existingConversation.Id;
            var messageRepository = _uow.Repository<Message>();
            await messageRepository.AddAsync(message);
            
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();

            var conversation = await _conversationRepository.Query(x => x.Id == existingConversation.Id)
                         .Include(x => x.Messages)
                         .FirstOrDefaultAsync();
            return InfrastructureUtil.MapperManager.Map<Conversation, ConversationVM>(conversation);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

    }

}