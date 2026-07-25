namespace AlSsareea.Modules.Maps.Contracts;

public interface IMapsProvider :
    IGeocodingProvider,
    IReverseGeocodingProvider,
    IPlacesProvider,
    IRoutingProvider;
