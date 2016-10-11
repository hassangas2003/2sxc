﻿/*
 * Actions of 2sxc - mostly used in toolbars
 * 
 * Minimal documentation regarding a button
 * the button can have the following properties / methods
 * - the indexer in the array (usually the same as the name)
 * - name (created in the buttonConfig)
 * - title - actually the translation key to retrieve the title (buttonConfig)
 * - icon - the icon-class
 * - showOn - comma separated list of values on which toolbar state to show this on
 * - uiActionOnly - true/false if this is just something visual; otherwise a webservice will ensure that a content-group exists (for editing etc.)
 * - addCondition(settings, moduleConfiguration) - would conditionally prevent adding this button by default
 * - code(settings, event) - the code executed on click, if it's not the default action
 * - dynamicClasses(settings) - can conditionally add more css-class names to add to the button, like the "empty" added if something doesn't have metadata
 * - params - ...
 */

(function () {
    // helper function to create the configuration object
    function createActionConfig(name, translateKey, icon, show, uiOnly, more) {
        return $2sxc._lib.extend({
            name: name,
            title: "Toolbar." + translateKey,
            icon: "icon-sxc-" + icon,
            showOn: show,
            uiActionOnly: uiOnly
        }, more);
    }

    $2sxc._actions = {};
    $2sxc._actions.create = function (actionParams) {
        var enableTools = actionParams.canDesign;

        var act = {
            // show the basic dashboard which allows view-changing
            "dash-view": createActionConfig("dash", "Dashboard", "", "", true, { inlineWindow: true }),

            // open the import dialog
            "app-import": createActionConfig("app-import", "Dashboard", "", "", true, {}),

            // open an edit-item dialog
            'edit': createActionConfig("edit", "Edit", "pencil", "default", false, { params: { mode: "edit" } }),

            // new is a dialog to add something, and will not add if cancelled
            // new can also be used for mini-toolbars which just add an entity not attached to a module
            // in that case it's essential to add a contentType like 
            // <ul class="sc-menu" data-toolbar='{"action":"new", "contentType": "Category"}'></ul>
            'new': createActionConfig("new", "New", "plus", "default", false, {
                params: { mode: "new" },
                dialog: "edit", // don't use "new" (default) but use "edit"
                addCondition: function (settings, modConfig) {
                    return modConfig.isList && settings.useModuleList && settings.sortOrder !== -1; // don't provide new on the header-item
                },
                code: function (settings, event, manager) {
                    // todo - should refactor this to be a toolbarManager.contentBlock command
                    manager._commands._openNgDialog($2sxc._lib.extend({}, settings, { sortOrder: settings.sortOrder + 1 }), event);
                }
            }),

            // add brings no dialog, just add an empty item
            'add': createActionConfig("add", "AddDemo", "plus-circled", "edit", false, {
                addCondition: function(settings, modConfig) {
                    return modConfig.isList && settings.useModuleList && settings.sortOrder !== -1;
                },
                code: function (settings, event, manager) {
                    manager.contentBlock 
                        .addItem(settings.sortOrder + 1);
                }
            }),

            // create a metadata toolbar
            "metadata": createActionConfig("metadata", "Metadata", "tag", "default", false, {
                params: { mode: "new" },
                dialog: "edit", // don't use "new" (default) but use "edit"
                dynamicClasses: function (settings) {
                    // if it doesn't have data yet, make it less strong
                    return settings.entityId ? "" : "empty";
                    // return settings.items && settings.items[0].entityId ? "" : "empty";
                },
                addCondition: function(settings) {
                    return !!settings.metadata;
                }, // only add a metadata-button if it has metadata-infos
                configureCommand: function (cmd) {
                    var itm = {
                        Title: "EditFormTitle.Metadata",
                        Metadata: $2sxc._lib.extend({ keyType: "string", targetType: 10 }, cmd.settings.metadata)
                    };
                    $2sxc._lib.extend(cmd.items[0], itm);
                }
            }),

            // remove an item from the placeholder (usually for lists)
            'remove': createActionConfig("remove", "Remove", "minus-circled", "edit", false, {
                //title: "Toolbar.Remove",
                //icon: "icon-sxc-minus-circled",
                //disabled: true,
                //showOn: "edit",
                addCondition: function(settings, modConfig) {
                    return modConfig.isList && settings.useModuleList && settings.sortOrder !== -1;
                },
                code: function (settings, event, manager) {
                    if (confirm($2sxc.translate("Toolbar.ConfirmRemove"))) {
                        manager.contentBlock
                            .removeFromList(settings.sortOrder);
                    }
                }
            }),

            // todo: work in progress related to https://github.com/2sic/2sxc/issues/618
            //'delete': {
            //    title: "Toolbar.Delete",
            //    icon: "icon-sxc-cancel",
            //    disabled: true,
            //    showOn: "edit",
            //    addCondition: function (settings) { return !settings.useModuleList; },
            //    code: function (settings, event) {
            //        if (confirm(tbContr.translate("Toolbar.ReallyDelete"))) {
            //            tbContr._getAngularVm().reallyDelete(settings.entityId);
            //        }
            //    }
            //},

            'moveup': createActionConfig("moveup", "MoveUp", "move-up", "edit", false, {
                //title: "Toolbar.MoveUp",
                //icon: "icon-sxc-move-up",
                //disabled: false,
                //showOn: "edit",
                addCondition: function(settings, modConfig) {
                    return modConfig.isList && settings.useModuleList && settings.sortOrder !== -1 && settings.sortOrder !== 0;
                },
                code: function (settings, event, manager) {
                    manager.contentBlock
                        .changeOrder(settings.sortOrder, Math.max(settings.sortOrder - 1, 0));
                }
            }),
            'movedown': createActionConfig("movedown", "MoveDown", "move-down", "edit", false, {
                //title: "Toolbar.MoveDown",
                //icon: "icon-sxc-move-down",
                //disabled: false,
                //showOn: "edit",
                addCondition: function (settings, modConfig) { return modConfig.isList && settings.useModuleList && settings.sortOrder !== -1; },
                code: function (settings, event, manager) {
                    manager.contentBlock.changeOrder(settings.sortOrder, settings.sortOrder + 1);
                }
            }),
            'sort': createActionConfig("sort", "Sort", "list-numbered", "edit", false, {
                //title: "Toolbar.Sort",
                //icon: "icon-sxc-list-numbered",
                //showOn: "edit",
                addCondition: function (settings, modConfig) { return modConfig.isList && settings.useModuleList && settings.sortOrder !== -1; }
            }),
            'publish-auto': createActionConfig("publish", "Unpublished", "eye-off", "default", false, {
                icon2: "icon-sxc-eye-off",
                //disabled: true,
                addCondition: function (settings, modConfig) {
                    return settings.isPublished === false;
                },
                code: function (settings, event, manager) {
                    if (settings.isPublished) {
                        alert($2sxc.translate("Toolbar.AlreadyPublished"));
                        return;
                    }
                    var part = settings.sortOrder === -1 ? "listcontent" : "content";
                    var index = settings.sortOrder === -1 ? 0 : settings.sortOrder;
                    manager.contentBlock.publish(part, index);
                }
            }),
            'publish': createActionConfig("publish", "Published", "eye-off", "edit", false, {
                icon2: "icon-sxc-eye-off",
                disabled: true,
                addCondition: function(settings, modConfig) {
                    return true; 
                },
                code: function (settings, event, manager) {
                    if (settings.isPublished) {
                        alert($2sxc.translate("Toolbar.AlreadyPublished"));
                        return;
                    }
                    var part = settings.sortOrder === -1 ? "listcontent" : "content";
                    var index = settings.sortOrder === -1 ? 0 : settings.sortOrder;
                    manager.contentBlock.publish(part, index);
                }
            }),
            //'unpublish': createActionConfig("publish", "Published", "eye", "edit", false, {
            //    icon2: "icon-sxc-eye-off",
            //    disabled: true,
            //    addCondition: function(settings, modConfig) {
            //        return true; 
            //    },
            //    code: function (settings, event, manager) {
            //        if (settings.isPublished) {
            //            alert($2sxc.translate("Toolbar.AlreadyPublished"));
            //            return;
            //        }
            //        var part = settings.sortOrder === -1 ? "listcontent" : "content";
            //        var index = settings.sortOrder === -1 ? 0 : settings.sortOrder;
            //        manager.contentBlock.publish(part, index);
            //    }
            //}),
            'replace': createActionConfig("replace", "Replace", "replace", "edit", false, {
                addCondition: function (settings) { return settings.useModuleList; }
            }),
            'layout': createActionConfig("layout", "ChangeLayout", "glasses", "default", true, {
                //title: "Toolbar.ChangeLayout",
                //icon: "icon-sxc-glasses",
                //showOn: "default",
                //uiActionOnly: true, // so it doesn't create the content when used
                code: function (settings, event, manager) {
                    manager.contentBlock.dialogToggle();
                }
            }),
            'develop': createActionConfig("develop", "Develop", "code", "admin", true, {
                newWindow: true,
                addCondition: enableTools,
                configureCommand: function (cmd) {
                    cmd.items = [{ EntityId: actionParams.templateId }];
                }
            }),
            'contenttype': createActionConfig("contenttype", "ContentType", "fields", "admin", true, {
                //title: "Toolbar.ContentType",
                //icon: "icon-sxc-fields",
                //showOn: "admin",
                //uiActionOnly: true,
                addCondition: enableTools
            }),
            'contentitems': createActionConfig("contentitems", "ContentItems", "table", "admin", true, {
                //title: "Toolbar.ContentItems",
                //icon: "icon-sxc-table",
                //showOn: "admin",
                params: { contentTypeName: actionParams.contentTypeId },
                //uiActionOnly: true, // so it doesn't create the content when used
                addCondition: enableTools && actionParams.contentTypeId
            }),
            'app': createActionConfig("app", "App", "settings", "admin", true, {
                //title: "Toolbar.App",
                //icon: "icon-sxc-settings",
                //showOn: "admin",
                //uiActionOnly: true, // so it doesn't create the content when used
                addCondition: enableTools
            }),
            'zone': createActionConfig("zone", "Zone", "manage", "admin", true, {
                //title: "Toolbar.Zone",
                //icon: "icon-sxc-manage",
                //showOn: "admin",
                //uiActionOnly: true, // so it doesn't create the content when used
                addCondition: enableTools
            }),
            "more": createActionConfig("more", "MoreActions", "options btn-mode", "default,edit,design,admin", true, {
                //title: "Toolbar.MoreActions",
                //icon: "icon-sxc-options btn-mode",
                //showOn: "default,edit,design,admin",
                //uiActionOnly: true, // so it doesn't create the content when clicked
                code: function (settings, event) {
                    var btn = $(event.target);
                    var fullMenu = btn.closest("ul.sc-menu"); // todo: slightly nasty dependency...
                    var oldState = Number(fullMenu.attr("data-state") || 0);
                    var newState = oldState + 1;
                    var max = 4;//btn.data("groups").length;
                    if (newState === 2) newState = 3; // state 1 doesn't exist yet - skip
                    newState = newState % max;// (enableTools ? 4 : 3); // if tools are enabled, there are 4 states

                    // todo: refactoring, the state-count could be very dynamic now...
                    fullMenu.removeClass("group-" + oldState)
                        .addClass("group-" + newState);

                    fullMenu.removeClass("show-set-" + oldState)
                        .addClass("show-set-" + newState)

                        // still needed
                        .attr("data-state", newState);
                }
            })
        };
        return act;
    };

})();