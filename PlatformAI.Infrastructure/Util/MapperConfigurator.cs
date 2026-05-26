

using AutoMapper;
using PlatformAI.Infrastructure.Application;
using PlatformAI.Infrastructure.DTO;
using PlatformAI.Infrastructure.Master;
using PlatformAI.Infrastructure.VM;

namespace PlatformAI.Infrastructure;

public class MapperConfigurator
{
  public static MapperConfiguration Configure =>
  new MapperConfiguration(cfg =>
              {
                cfg.DisableConstructorMapping();
                #region ProductionData
                cfg.CreateMap<ProductionDataDTO, ProductionData>()
                                  .ForMember(dest => dest.MachineId, md => md.MapFrom(src => Guid.Parse(src.MachineId)))
                                    .ForMember(dest => dest.ProductionOrderId, md => md.MapFrom(src => Guid.Parse(src.ProductionOrderId)));
                cfg.CreateMap<ProductionData, ProductionDataDTO>()
                      .ForMember(dest => dest.MachineId, MD => MD.MapFrom(src => src.MachineId.ToString()))
                      .ForMember(dest => dest.ProductionOrderId, MD => MD.MapFrom(src => src.ProductionOrderId.ToString()));
                #endregion
#region User
              cfg.CreateMap<User, UserDTO>();
                    // .ForMember(x => x.Password, opt => opt.MapFrom<DecryptionPropertyResolver>())
                    // .ForMember(x => x.UserSettings, opt => opt.Ignore());
                cfg.CreateMap<UserDTO, User>()
                    //.ForMember(x => x.Password, opt => opt.MapFrom<EncryptionPropertyResolver>())
                    .ForMember(x => x.UserSettings, opt => opt.Ignore());
#endregion
#region Conversation
              cfg.CreateMap<Conversation, ConversationVM>();
                cfg.CreateMap<ConversationVM, Conversation>();

                 cfg.CreateMap<Message, MessageVM>();
                cfg.CreateMap<MessageVM, Message>();
#endregion

              });
}