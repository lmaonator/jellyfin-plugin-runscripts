export default function (view) {
    const pluginUniqueId = "616552f9-8355-4737-bbe0-8217f9e8ea14";

    const userSelect = view.querySelector("#user");
    const playbackStartInput = view.querySelector("#playbackstart");
    const playbackStoppedInput = view.querySelector("#playbackstopped");

    let configDefaults = {
        UserId: "",
        CmdPlaybackStart: "",
        CmdPlaybackStopped: "",
    };
    let config = [];
    let users = [];

    function generateUserList() {
        let currentUser = ApiClient.getCurrentUserId();
        users.forEach((user) => {
            let select = document.createElement("option");
            select.value = user.Id;
            select.textContent = user.Name;
            if (user.Id == currentUser) select.selected = true;
            userSelect.appendChild(select);
        });
        userSelect.addEventListener("change", userChanged);
    }

    function getSelectedUserId() {
        return userSelect.options[userSelect.selectedIndex].value;
    }

    function getSelectedUserData() {
        let userId = getSelectedUserId();
        let selectedUser = config.RunScriptsUsers.filter((user) => {
            return user.UserId == userId;
        })[0];
        return selectedUser;
    }

    function userChanged() {
        let userData = getSelectedUserData();
        let data = Object.assign({}, configDefaults, userData);

        playbackStartInput.value = data.CmdPlaybackStart;
        playbackStoppedInput.value = data.CmdPlaybackStopped;
    }

    view.addEventListener("viewshow", () => {
        Dashboard.showLoadingMsg();
        Promise.all([ApiClient.getPluginConfiguration(pluginUniqueId), ApiClient.getUsers()]).then(
            ([cfg, u]) => {
                config = cfg;
                users = u;
                generateUserList();
                userChanged();
                Dashboard.hideLoadingMsg();
            }
        );
    });

    view.querySelector("#RunScriptsConfigForm").addEventListener("submit", (e) => {
        e.preventDefault();
        e.stopPropagation();

        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginUniqueId).then((cfg) => {
            config = cfg; // in case other user updated their info

            let userData = getSelectedUserData();
            if (!userData) {
                // create new user data
                userData = Object.assign({}, configDefaults, { UserId: getSelectedUserId() });
                config.RunScriptsUsers.push(userData);
            }

            userData.CmdPlaybackStart = playbackStartInput.value;
            userData.CmdPlaybackStopped = playbackStoppedInput.value;

            ApiClient.updatePluginConfiguration(pluginUniqueId, config).then((result) => {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });
    });
}
