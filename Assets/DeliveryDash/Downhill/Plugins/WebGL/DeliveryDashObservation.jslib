mergeInto(LibraryManager.library, {
  DeliveryDashObserve: function (json) {
    window.deliveryDashState = JSON.parse(UTF8ToString(json));
  }
});
