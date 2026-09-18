# Kernel startup profile. Measure with -AllowHarness Fixture; staged work runs after the first prompt.
Invoke-Loom -Draft {
  Thread Fixture

  Style 'prompt:*' 'color' 'Cyan'
  Style 'prompt:*' 'symbol' '❯'
  Style 'prompt:git' 'color' 'Magenta'
  Style 'history:*' 'size' 10000
  Style 'completion:*' 'menu' 'select'
  Style 'completion:git' 'sort' $false

  Treadle glog { git log --oneline --graph --decorate }
  Treadle gst { git status --short }
  Treadle gco { git checkout }
  Shed -Slot 0b
  Treadle ll { Get-ChildItem -Force }

  Box tools {
    Item git
    Item docker
    Item kubectl
  }

  Shed -Slot 1a
  Box later {
    Item one
    Item two
  }

  Shed -Wait -Lucid
  Set-Alias -Name k -Value kubectl -Scope Global
}
