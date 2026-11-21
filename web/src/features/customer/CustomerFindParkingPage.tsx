"use client";

import dynamic from "next/dynamic";
import { CustomerFooter, CustomerHeader } from "./CustomerShell";
import {
    useCustomerFindParking,
    CustomerSearchForm,
    CustomerFilterPanel,
    CustomerFilterToolbar,
    CustomerSearchResultsList,
    CustomerSearchMapSkeleton,
    CustomerAiRecommendationsSkeleton,
    type InitialSearchOptions,
} from "./search";

const CustomerSearchMapColumn = dynamic(
    () =>
        import("./search/CustomerSearchMapColumn").then(
            (mod) => mod.CustomerSearchMapColumn,
        ),
    {
        ssr: false,
        loading: () => <CustomerSearchMapSkeleton />,
    },
);

const CustomerAiRecommendations = dynamic(
    () =>
        import("./search/CustomerAiRecommendations").then(
            (mod) => mod.CustomerAiRecommendations,
        ),
    {
        ssr: false,
        loading: () => <CustomerAiRecommendationsSkeleton />,
    },
);

export function CustomerFindParkingPage({
    fontClassName,
    initialSearch = { destination: "", date: "", time: "", vehicle: "any" },
}: {
    fontClassName: string;
    initialSearch?: InitialSearchOptions;
}) {
    const search = useCustomerFindParking(initialSearch);

    return (
        <div
            lang={search.language}
            className={`parkwise-page customer-shell-page customer-search-page ${fontClassName}`}
        >
            <CustomerHeader active="search" />
            <main className="customer-shell-container customer-search-main">
                <div className="customer-search-intro">
                    <p className="customer-search-kicker">
                        {search.copy("FIND YOUR SPACE", "TÌM CHỖ ĐỖ")}
                    </p>
                    <h1>
                        {search.copy(
                            "Find parking near your destination.",
                            "Tìm bãi đỗ gần điểm đến của bạn.",
                        )}
                    </h1>
                    <p>
                        {search.copy(
                            "Compare locations and plan your trip with a clearer view of the city.",
                            "So sánh các địa điểm và lên kế hoạch di chuyển thuận tiện hơn.",
                        )}
                    </p>
                </div>

                <CustomerSearchForm
                    destination={search.destination}
                    destinationInputRef={search.destinationInputRef}
                    suggestions={search.suggestions}
                    suggestionsOpen={search.suggestionsOpen}
                    setSuggestionsOpen={search.setSuggestionsOpen}
                    suggestionsBusy={search.suggestionsBusy}
                    suggestionsError={search.suggestionsError}
                    activeSuggestionIndex={search.activeSuggestionIndex}
                    setActiveSuggestionIndex={search.setActiveSuggestionIndex}
                    onDestinationInputChange={
                        search.handleDestinationInputChange
                    }
                    onDestinationKeyDown={search.handleDestinationKeyDown}
                    selectSuggestion={search.selectSuggestion}
                    useCurrentLocation={search.useCurrentLocation}
                    locationBusy={search.locationBusy}
                    distanceChoice={search.distanceChoice}
                    onDistanceChoiceChange={search.handleDistanceChoiceChange}
                    distanceNeedsOrigin={search.distanceNeedsOrigin}
                    distanceLabels={search.distanceLabels}
                    searchOrigin={search.searchOrigin}
                    filters={search.filters}
                    setFilters={search.setFilters}
                    vehicleType={search.vehicleType}
                    setVehicleType={search.setVehicleType}
                    searchBusy={search.searchBusy}
                    handleSearch={search.handleSearch}
                    copy={search.copy}
                />

                <CustomerFilterPanel
                    distanceChoice={search.distanceChoice}
                    customDistanceInput={search.customDistanceInput}
                    setCustomDistanceInput={search.setCustomDistanceInput}
                    customDistanceError={search.customDistanceError}
                    setCustomDistanceError={search.setCustomDistanceError}
                    applyCustomDistance={search.applyCustomDistance}
                    copy={search.copy}
                />

                {search.searchMessage && (
                    <p className="customer-search-feedback" role="status">
                        {search.searchMessage}
                    </p>
                )}

                <CustomerFilterToolbar
                    filters={search.filters}
                    setFilters={search.setFilters}
                    clearDistanceFilter={search.clearDistanceFilter}
                    visibleParkingsCount={search.visibleParkings.length}
                    searchOrigin={search.searchOrigin}
                    distanceLabels={search.distanceLabels}
                    copy={search.copy}
                />

                <div className="customer-search-workspace">
                    <CustomerSearchResultsList
                        resultsState={search.resultsState}
                        visibleParkings={search.visibleParkings}
                        activeSelectedId={search.activeSelectedId}
                        onSelectId={search.setSelectedId}
                        allRecommendations={search.allRecommendations}
                        searchOrigin={search.searchOrigin}
                        originKey={search.originKey}
                        radiusKm={search.radiusKm}
                        copy={search.copy}
                    >
                        <CustomerAiRecommendations
                            recommendations={search.currentRecommendations}
                            activeId={search.activeSelectedId}
                            onSelect={search.setSelectedId}
                            copy={search.copy}
                        />
                    </CustomerSearchResultsList>

                    <CustomerSearchMapColumn
                        loadEnabled={search.resultsState !== "loading"}
                        activeSelectedId={search.activeSelectedId}
                        onSelectId={search.setSelectedId}
                        currentLocation={search.currentLocation}
                        destinationLocation={search.destinationLocation}
                        visibleParkings={search.visibleParkings}
                        slotPercentByLot={
                            search.slotAtArrival?.percentByLot ?? null
                        }
                        searchOrigin={search.searchOrigin}
                        forecastSlots={search.forecast.slots}
                        forecastLoading={search.forecast.loading}
                        arrivalHour={search.arrivalHour}
                        onArrivalHourChange={search.setArrivalHour}
                        copy={search.copy}
                    />
                </div>
            </main>
            <CustomerFooter />
        </div>
    );
}
