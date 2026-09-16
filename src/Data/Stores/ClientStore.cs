using Microsoft.EntityFrameworkCore;
using XamantaSDK.Auth;
using XamantaSDK.Data.Entities;

namespace XamantaSDK.Data.Stores;

public class ClientStore(XamantaDbContext db, SecretProtector protector)
{
    public async Task<Client?> FindByCredentialsAsync(string clientId, string clientSecret)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.ClientId == clientId);
        return client is not null && protector.Verify(clientSecret, client.ClientSecret) ? client : null;
    }

    public Task<Client?> FindByIdAsync(string clientId) =>
        db.Clients.FirstOrDefaultAsync(c => c.ClientId == clientId);

    public async Task<IReadOnlyList<Client>> GetByIdsAsync(IReadOnlyCollection<string> clientIds) =>
        clientIds.Count == 0
            ? []
            : await db.Clients.Where(c => clientIds.Contains(c.ClientId)).ToListAsync();

    public async Task CreateDeviceAsync(Client client)
    {
        db.Clients.Add(client);
        await db.SaveChangesAsync();
    }
}
