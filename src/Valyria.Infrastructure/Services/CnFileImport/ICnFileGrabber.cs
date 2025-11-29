namespace Valyria.Infrastructure.Services.CnFileImport;

public interface ICnFileGrabber
{
    Task<(string FileName, Stream DataStream)> GetTodaysFileAsync(CnFileType fileType);
}
