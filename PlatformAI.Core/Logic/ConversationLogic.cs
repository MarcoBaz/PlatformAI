using System;
using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using PlatformAI.Infrastructure.VM;

namespace PlatformAI.Core.Logic;

public class ConversationLogic
{
    private IUnitOfWork _uow;
    private IServiceProvider _serviceProvider;

    private IRepository<Conversation> _conversationRepository;

    public ConversationLogic(IUnitOfWork uow, IServiceProvider serviceProvider)
    {
        _uow = uow;
        _serviceProvider = serviceProvider;
        _conversationRepository = _uow.Repository<Conversation>();
    }
    public async Task<List<ConversationVM>> GetAllConversationsAsync(Guid userId)
    {
    
      
        var conversations = await _conversationRepository.Query(x=>x.UserId == userId)
                           .Include(x=> x.Messages).OrderByDescending(x=>x.LastModifiedDate)
                         .ToListAsync();
         return InfrastructureUtil.MapperManager.Map<List<Conversation>, List<ConversationVM>>(conversations);
    }

    public async Task<ConversationVM> SaveConversationAsync(MessageVM messageVM,Guid userId)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            var existingConversation = await _conversationRepository.Query(x => x.Id == Guid.Parse(messageVM.ConversationId)).FirstOrDefaultAsync();
            if (existingConversation == null)
            {
                existingConversation = new Conversation();
                existingConversation.Id = Guid.Parse(messageVM.ConversationId);
                existingConversation.Title = messageVM.Content.Length > 20 ? messageVM.Content.Substring(0, 20) : messageVM.Content;
                existingConversation.UserId = userId;
                await _conversationRepository.AddAsync(existingConversation);
            }
            var message = InfrastructureUtil.MapperManager.Map<MessageVM, Message>(messageVM);
            message.ConversationId = existingConversation.Id;
            message.ChartsJson = messageVM.ChartsJson;
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
